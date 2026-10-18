package kittoku.osc.traffic

import java.util.Locale


private val BYTE_UNITS = arrayOf("B", "KB", "MB", "GB", "TB")
private val SPEED_UNITS = arrayOf("B/s", "KB/s", "MB/s", "GB/s", "TB/s")

private fun scale(raw: Double, units: Array<String>): String {
    var value = raw.coerceAtLeast(0.0)
    var index = 0

    while (value >= 1024.0 && index < units.size - 1) {
        value /= 1024.0
        index++
    }

    val pattern = when {
        index == 0 -> "%.0f"
        value < 10.0 -> "%.2f"
        value < 100.0 -> "%.1f"
        else -> "%.0f"
    }

    return String.format(Locale.US, pattern, value) + " " + units[index]
}

internal fun formatBytes(bytes: Double): String = scale(bytes, BYTE_UNITS)

internal fun formatSpeed(bytesPerSecond: Double): String = scale(bytesPerSecond, SPEED_UNITS)
