package com.lux.ticker.data

import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.longPreferencesKey
import com.lux.ticker.account.CloudSync
import com.lux.ticker.backup.BackupPolicy
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File

class SettingsUpgradeTest {
    @Test fun upgradeFromSavedPrefsLeavesEverySettingAlone() = runBlocking {
        val (store, _) = seeded()
        val saved = store.read()
        val clock = store.localUpdatedAt()
        assertTrue(clock > 0L)
        assertTrue(saved != TickerSettings())
        store.ensureFactoryDefaults()
        assertEquals(saved, store.read())
        assertEquals(clock, store.localUpdatedAt())
        assertTrue(store.hasStoredSettings())
    }

    @Test fun clocklessSavedPrefsStayPutAndPush() = runBlocking {
        val (store, data) = seeded()
        data.edit { it.remove(longPreferencesKey("sync_updated_at")) }
        assertEquals(0L, store.localUpdatedAt())
        assertTrue(store.hasStoredSettings())
        val saved = store.read()
        store.ensureFactoryDefaults()
        assertEquals(saved, store.read())
        assertEquals(0L, store.localUpdatedAt())
        assertEquals(
            CloudSync.Decision.PUSH,
            CloudSync.decide(0L, 9_000L, "local", """{"settings":{"dock":"BOTTOM"}}""", localHasSettings = true),
        )
    }

    @Test fun aUsedFileDoesNotGrowAMissingOffset() = runBlocking {
        val file = File.createTempFile("lux-upgrade-offset", ".preferences_pb")
        file.delete()
        val store = SettingsStore(PreferenceDataStoreFactory.create { file })
        store.setSpeed(80f)
        assertFalse(store.hasStoredVerticalOffset())
        val clock = store.localUpdatedAt()
        store.ensureFactoryDefaults()
        assertFalse(store.hasStoredVerticalOffset())
        assertEquals(80f, store.read().speedDpPerSec, 0.001f)
        assertEquals(VerticalOffset.DEFAULT_DP, store.read().verticalOffsetDp, 0.001f)
        assertEquals(clock, store.localUpdatedAt())
    }

    @Test fun partialCloudPullWritesOnlyKeysTheServerSent() = runBlocking {
        val (store, _) = seeded()
        val saved = store.read()
        val raw = """{"v":1,"settings":{"speedDpPerSec":90}}"""
        val merged = CloudSync.merge(saved, raw)!!
        assertNull(merged.symbols)
        assertEquals(saved.copy(speedDpPerSec = 90f), merged.settings)
        store.replaceFromCloud(merged.settings, 50L)
        assertEquals(saved.copy(speedDpPerSec = 90f), store.read())
        assertEquals(50L, store.localUpdatedAt())
    }

    @Test fun backupKeepsAnExistingDatastoreAndAcceptsAMissingOne() {
        assertTrue(BackupPolicy.keepLocalFile("/data/data/com.lux.ticker/files/datastore/lux_settings.preferences_pb", 128L))
        assertFalse(BackupPolicy.keepLocalFile("/data/data/com.lux.ticker/files/datastore/lux_settings.preferences_pb", 0L))
        assertFalse(BackupPolicy.keepLocalFile("/data/data/com.lux.ticker/shared_prefs/lux_account_session.xml", 128L))
    }

    private fun seeded(): Pair<SettingsStore, androidx.datastore.core.DataStore<androidx.datastore.preferences.core.Preferences>> {
        val file = File.createTempFile("lux-upgrade", ".preferences_pb")
        file.delete()
        val data = PreferenceDataStoreFactory.create { file }
        val store = SettingsStore(data)
        runBlocking {
            store.setDock(Dock.TOP)
            store.setSpeed(80f)
            store.setHeight(28f)
            store.setStartOnBoot(false)
            store.setOverlay(false)
            store.setShowHandle(true)
            store.setLeftPad(90f)
            store.setRightPad(120f)
            store.setShowOnLockScreen(false)
            store.setOrangeDividers(true)
            store.setLuxWirePosts(false)
            store.setLuxWirePollMinutes(5)
            store.setPinBtc(false)
            store.setTextSize(16f)
            store.setVerticalOffset(12.5f)
            store.setShowSessionTags(false)
            store.setSymbolColor(TapeColors.PRICE)
            store.setPriceColor(PaperPalette.INK)
            store.setChartHaptics(false)
            store.setStripTheme(StripTheme.LIGHT)
            store.setAppTheme(AppTheme.DARK)
            store.setDisplayMode(DisplayMode.LOCK_ONLY)
            store.setLuxWireDigest(true)
            store.setLuxWireSubscriber(false)
            store.setPriceAlerts(true)
            store.setPriceAlertsLoud(true)
            store.setBtcAlertPct(7f)
            store.setPriceAlertSymbol("MSTR")
            store.setPriceAlertTarget(400f)
            store.setScrollRightToLeft(false)
            store.setPinSide(PinSide.RIGHT)
            store.addAlert(SavedAlert("a1", AlertKind.PRICE, "MSTR", 250.0))
        }
        return store to data
    }
}
