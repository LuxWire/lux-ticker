package com.lux.ticker.account

import android.net.Uri
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

/** Deep link handoff. The callback activity and [com.lux.ticker.MainActivity] both offer the uri. */
object AccountEvents {
    private val pending = MutableStateFlow<Uri?>(null)
    val uris: StateFlow<Uri?> = pending

    fun offer(uri: Uri) {
        pending.value = uri
    }

    fun consume(): Uri? {
        val uri = pending.value ?: return null
        pending.value = null
        return uri
    }
}
