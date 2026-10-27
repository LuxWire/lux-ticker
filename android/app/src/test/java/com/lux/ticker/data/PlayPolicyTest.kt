package com.lux.ticker.data

import com.lux.ticker.ui.ReleaseNotes
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File

class PlayPolicyTest {
    @Test fun manifestKeepsTheOverlayAndDropsTheBatteryExemption() {
        val xml = manifest()
        assertTrue(xml.contains("android.permission.SYSTEM_ALERT_WINDOW"))
        assertTrue(xml.contains("android.permission.FOREGROUND_SERVICE_SPECIAL_USE"))
        assertTrue(xml.contains("android.permission.POST_NOTIFICATIONS"))
        assertTrue(xml.contains("android.permission.VIBRATE"))
        assertTrue(xml.contains("android.permission.INTERNET"))
        assertTrue(xml.contains("foregroundServiceType=\"specialUse\""))
        assertTrue(xml.contains("PROPERTY_SPECIAL_USE_FGS_SUBTYPE"))
        assertFalse(xml.contains("QUERY_ALL_PACKAGES"))
        assertFalse(xml.contains("REQUEST_IGNORE_BATTERY_OPTIMIZATIONS"))
        assertFalse(xml.contains("AD_ID"))
        assertFalse(xml.contains("ACCESS_FINE_LOCATION"))
        assertFalse(xml.contains("ACCESS_COARSE_LOCATION"))
        assertFalse(xml.contains("READ_CONTACTS"))
        assertTrue(xml.contains("lux_wire_auth.xml"))
        assertTrue(xml.contains("lux_account_session.xml"))
        assertTrue(xml.contains("luxwire"))
        assertTrue(xml.contains("auth-callback"))
        assertTrue(ReleaseNotes.ABOUT.contains("Hermes Lux"))
        assertTrue(ReleaseNotes.ABOUT.contains("does not sell data"))
        assertTrue(ReleaseNotes.WHY_OVERLAY.contains("does not read the screen"))
        assertTrue(ReleaseNotes.WHY_NOTIFICATIONS.contains("ticker tape"))
        assertFalse(ReleaseNotes.ABOUT.contains("Jonathan"))
    }

    private fun manifest(): String {
        val candidates = listOf(
            File("src/main/AndroidManifest.xml"),
            File("app/src/main/AndroidManifest.xml"),
        )
        val file = candidates.first { it.isFile }
        val backup = File(file.parentFile, "res/xml/data_extraction_rules.xml")
        return file.readText() + backup.readText()
    }
}
