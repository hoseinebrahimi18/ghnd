package kittoku.osc.traffic

import android.content.Context
import android.graphics.Canvas
import android.graphics.Paint
import android.graphics.Path
import android.util.AttributeSet
import android.view.View
import androidx.core.content.ContextCompat
import kittoku.osc.R


private const val GRID_LINES = 4
private const val MIN_AXIS_MAX = 8.0 * 1024.0 // 8 KB/s, keeps an idle graph calm
private val NICE_STEPS = doubleArrayOf(1.0, 2.0, 5.0, 10.0, 20.0, 50.0, 100.0, 200.0, 500.0, 1000.0, 1024.0)

internal class TrafficGraphView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null
) : View(context, attrs) {
    private val density = resources.displayMetrics.density
    private val scaledDensity = resources.displayMetrics.scaledDensity

    private val downColor = ContextCompat.getColor(context, R.color.traffic_down)
    private val upColor = ContextCompat.getColor(context, R.color.traffic_up)

    private val textColor: Int = context.obtainStyledAttributes(
        intArrayOf(android.R.attr.textColorSecondary)
    ).let {
        val color = it.getColor(0, 0xFF888888.toInt())
        it.recycle()
        color
    }

    private val gridPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = (textColor and 0x00FFFFFF) or 0x33000000
        strokeWidth = density
        style = Paint.Style.STROKE
    }

    private val labelPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = textColor
        textSize = 10f * scaledDensity
        textAlign = Paint.Align.RIGHT
    }

    private val footerLeftPaint = Paint(labelPaint).apply { textAlign = Paint.Align.LEFT }
    private val footerRightPaint = Paint(labelPaint).apply { textAlign = Paint.Align.RIGHT }

    private val downLinePaint = linePaint(downColor)
    private val upLinePaint = linePaint(upColor)
    private val downFillPaint = fillPaint(downColor)
    private val upFillPaint = fillPaint(upColor)

    private val linePath = Path()
    private val fillPath = Path()

    private var samples: List<TrafficSample> = emptyList()

    internal var windowSeconds: Int = 60
        set(value) {
            field = value.coerceAtLeast(2)
            invalidate()
        }

    internal fun setSamples(newSamples: List<TrafficSample>) {
        samples = newSamples
        invalidate()
    }

    private fun linePaint(color: Int) = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        this.color = color
        style = Paint.Style.STROKE
        strokeWidth = 2f * density
        strokeJoin = Paint.Join.ROUND
    }

    private fun fillPaint(color: Int) = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        this.color = (color and 0x00FFFFFF) or 0x40000000
        style = Paint.Style.FILL
    }

    private fun niceMax(peak: Long): Double {
        if (peak <= MIN_AXIS_MAX) return MIN_AXIS_MAX

        var unit = 1.0
        while (peak / unit >= 1024.0) unit *= 1024.0

        val scaled = peak / unit
        val step = NICE_STEPS.firstOrNull { it >= scaled } ?: NICE_STEPS.last()

        return step * unit
    }

    private fun windowLabel(): String {
        return if (windowSeconds >= 60) "-${windowSeconds / 60} min" else "-$windowSeconds s"
    }

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)

        val left = labelPaint.measureText("000.0 MB/s") + 8f * density
        val top = 10f * density
        val right = width - 8f * density
        val bottom = height - 20f * density
        if (right <= left || bottom <= top) return

        val window = windowSeconds
        val visible = if (samples.size > window) samples.subList(samples.size - window, samples.size) else samples

        var peak = 0L
        visible.forEach { peak = maxOf(peak, it.rxBytesPerSecond, it.txBytesPerSecond) }
        val axisMax = niceMax(peak)

        for (i in 0..GRID_LINES) {
            val y = bottom - (bottom - top) * i / GRID_LINES
            canvas.drawLine(left, y, right, y, gridPaint)
            canvas.drawText(
                formatSpeed(axisMax * i / GRID_LINES),
                left - 4f * density,
                y + labelPaint.textSize / 3f,
                labelPaint
            )
        }

        val footerY = height - 4f * density
        canvas.drawText(windowLabel(), left, footerY, footerLeftPaint)
        canvas.drawText("now", right, footerY, footerRightPaint)

        drawSeries(canvas, visible, left, right, top, bottom, axisMax, window, true)
        drawSeries(canvas, visible, left, right, top, bottom, axisMax, window, false)
    }

    private fun drawSeries(
        canvas: Canvas,
        visible: List<TrafficSample>,
        left: Float,
        right: Float,
        top: Float,
        bottom: Float,
        axisMax: Double,
        window: Int,
        isDownload: Boolean
    ) {
        if (visible.isEmpty()) return

        val stepX = (right - left) / (window - 1)
        val count = visible.size

        linePath.reset()
        fillPath.reset()

        for (i in 0 until count) {
            val value = if (isDownload) visible[i].rxBytesPerSecond else visible[i].txBytesPerSecond
            val x = right - (count - 1 - i) * stepX
            val ratio = (value / axisMax).coerceIn(0.0, 1.0).toFloat()
            val y = bottom - (bottom - top) * ratio

            if (i == 0) {
                linePath.moveTo(x, y)
                fillPath.moveTo(x, bottom)
                fillPath.lineTo(x, y)
            } else {
                linePath.lineTo(x, y)
                fillPath.lineTo(x, y)
            }
        }

        fillPath.lineTo(right, bottom)
        fillPath.close()

        canvas.drawPath(fillPath, if (isDownload) downFillPaint else upFillPaint)
        canvas.drawPath(linePath, if (isDownload) downLinePaint else upLinePaint)
    }
}
