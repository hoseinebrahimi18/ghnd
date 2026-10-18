package kittoku.osc.preference.custom

import android.content.Context
import android.util.AttributeSet
import android.view.View
import android.widget.TextView
import androidx.preference.Preference
import androidx.preference.PreferenceViewHolder
import com.google.android.material.button.MaterialButtonToggleGroup
import kittoku.osc.R
import kittoku.osc.traffic.TrafficGraphView
import kittoku.osc.traffic.TrafficMonitor
import kittoku.osc.traffic.formatBytes
import kittoku.osc.traffic.formatSpeed


internal class HomeTrafficPreference(context: Context, attrs: AttributeSet) : Preference(context, attrs), TrafficMonitor.Listener {
    private class Views(
        val downSpeed: TextView,
        val downTotal: TextView,
        val upSpeed: TextView,
        val upTotal: TextView,
        val graph: TrafficGraphView,
        val idleHint: TextView,
    )

    private var views: Views? = null

    init {
        layoutResource = R.layout.preference_traffic
        isSelectable = false
        isPersistent = false
    }

    override fun onAttached() {
        super.onAttached()

        TrafficMonitor.addListener(this)
    }

    override fun onDetached() {
        TrafficMonitor.removeListener(this)

        super.onDetached()
    }

    override fun onBindViewHolder(holder: PreferenceViewHolder) {
        super.onBindViewHolder(holder)

        holder.itemView.isClickable = false

        views = Views(
            holder.findViewById(R.id.traffic_down_speed) as TextView,
            holder.findViewById(R.id.traffic_down_total) as TextView,
            holder.findViewById(R.id.traffic_up_speed) as TextView,
            holder.findViewById(R.id.traffic_up_total) as TextView,
            holder.findViewById(R.id.traffic_graph) as TrafficGraphView,
            holder.findViewById(R.id.traffic_idle_hint) as TextView,
        )

        val group = holder.findViewById(R.id.traffic_range_group) as MaterialButtonToggleGroup
        group.clearOnButtonCheckedListeners()
        group.check(
            when (TrafficMonitor.selectedWindowSeconds) {
                1800 -> R.id.traffic_range_30m
                300 -> R.id.traffic_range_5m
                else -> R.id.traffic_range_1m
            }
        )
        group.addOnButtonCheckedListener { _, checkedId, isChecked ->
            if (!isChecked) return@addOnButtonCheckedListener

            TrafficMonitor.selectedWindowSeconds = when (checkedId) {
                R.id.traffic_range_30m -> 1800
                R.id.traffic_range_5m -> 300
                else -> 60
            }

            refresh()
        }

        refresh()
    }

    override fun onTrafficUpdated() {
        refresh()
    }

    private fun refresh() {
        val v = views ?: return

        v.downSpeed.text = formatSpeed(TrafficMonitor.currentRxSpeed.toDouble())
        v.upSpeed.text = formatSpeed(TrafficMonitor.currentTxSpeed.toDouble())
        v.downTotal.text = context.getString(R.string.traffic_total, formatBytes(TrafficMonitor.totalRxBytes.toDouble()))
        v.upTotal.text = context.getString(R.string.traffic_total, formatBytes(TrafficMonitor.totalTxBytes.toDouble()))

        v.graph.windowSeconds = TrafficMonitor.selectedWindowSeconds
        v.graph.setSamples(TrafficMonitor.snapshot())

        v.idleHint.visibility = if (TrafficMonitor.isActive) View.GONE else View.VISIBLE
    }
}
