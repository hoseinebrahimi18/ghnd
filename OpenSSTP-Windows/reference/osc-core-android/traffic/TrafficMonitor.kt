package kittoku.osc.traffic

import android.os.Handler
import android.os.Looper
import android.os.SystemClock
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.atomic.AtomicLong


internal data class TrafficSample(val rxBytesPerSecond: Long, val txBytesPerSecond: Long)

/**
 * Process-wide traffic statistics of the SSTP tunnel.
 *
 * rx (download) = bytes written to the TUN interface (IP packets delivered to apps)
 * tx (upload)   = bytes read from the TUN interface (IP packets sent by apps)
 *
 * The counters are fed by IPTerminal, so the values are the real amount of data
 * passing through the tunnel. Sampling runs only between start() and stop().
 */
internal object TrafficMonitor {
    internal const val SAMPLE_INTERVAL_MS = 1000L
    internal const val MAX_SAMPLES = 1800 // 30 minutes

    internal fun interface Listener {
        fun onTrafficUpdated() // always called on the main thread
    }

    private val lock = Any()
    private val handler = Handler(Looper.getMainLooper())
    private val listeners = CopyOnWriteArrayList<Listener>()

    private val rxCounter = AtomicLong(0)
    private val txCounter = AtomicLong(0)
    private val samples = ArrayDeque<TrafficSample>()

    private var lastRx = 0L
    private var lastTx = 0L
    private var lastTickTime = 0L

    @Volatile
    internal var isActive = false
        private set

    @Volatile
    internal var currentRxSpeed = 0L
        private set

    @Volatile
    internal var currentTxSpeed = 0L
        private set

    // the time range chosen in the UI; kept here so it survives view re-creation
    @Volatile
    internal var selectedWindowSeconds = 60

    internal val totalRxBytes: Long
        get() = rxCounter.get()

    internal val totalTxBytes: Long
        get() = txCounter.get()

    private val ticker = object : Runnable {
        override fun run() {
            if (!isActive) return

            sampleOnce()
            handler.postDelayed(this, SAMPLE_INTERVAL_MS)
        }
    }

    internal fun addRx(size: Int) {
        if (isActive && size > 0) rxCounter.addAndGet(size.toLong())
    }

    internal fun addTx(size: Int) {
        if (isActive && size > 0) txCounter.addAndGet(size.toLong())
    }

    /** Call when the tunnel is established. Counters and history start from zero. */
    internal fun start() {
        synchronized(lock) {
            rxCounter.set(0)
            txCounter.set(0)
            lastRx = 0
            lastTx = 0
            samples.clear()
            currentRxSpeed = 0
            currentTxSpeed = 0
            lastTickTime = SystemClock.elapsedRealtime()
            isActive = true
        }

        handler.removeCallbacks(ticker)
        handler.postDelayed(ticker, SAMPLE_INTERVAL_MS)
        notifyListeners()
    }

    /** Call when the connection is lost or closed. Sampling stops and the graph is cleared. */
    internal fun stop() {
        synchronized(lock) {
            isActive = false
            samples.clear()
            currentRxSpeed = 0
            currentTxSpeed = 0
            rxCounter.set(0)
            txCounter.set(0)
        }

        handler.removeCallbacks(ticker)
        notifyListeners()
    }

    internal fun snapshot(): List<TrafficSample> {
        synchronized(lock) {
            return samples.toList()
        }
    }

    internal fun addListener(listener: Listener) {
        if (!listeners.contains(listener)) listeners.add(listener)
    }

    internal fun removeListener(listener: Listener) {
        listeners.remove(listener)
    }

    private fun sampleOnce() {
        synchronized(lock) {
            if (!isActive) return

            val now = SystemClock.elapsedRealtime()
            val elapsed = (now - lastTickTime).coerceAtLeast(1L)
            val rx = rxCounter.get()
            val tx = txCounter.get()

            currentRxSpeed = (rx - lastRx) * 1000L / elapsed
            currentTxSpeed = (tx - lastTx) * 1000L / elapsed

            lastRx = rx
            lastTx = tx
            lastTickTime = now

            samples.addLast(TrafficSample(currentRxSpeed, currentTxSpeed))
            while (samples.size > MAX_SAMPLES) {
                samples.removeFirst()
            }
        }

        notifyListeners()
    }

    private fun notifyListeners() {
        if (Looper.myLooper() == Looper.getMainLooper()) {
            listeners.forEach { it.onTrafficUpdated() }
        } else {
            handler.post { listeners.forEach { it.onTrafficUpdated() } }
        }
    }
}
