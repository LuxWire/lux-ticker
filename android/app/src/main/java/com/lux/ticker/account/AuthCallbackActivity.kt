package com.lux.ticker.account

import android.app.Activity
import android.content.Intent
import android.os.Bundle
import com.lux.ticker.MainActivity

/** Browser return for Supabase OAuth. Forwards luxwire://auth-callback and closes. */
class AuthCallbackActivity : Activity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val forward = Intent(this, MainActivity::class.java).apply {
            data = intent?.data
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or
                Intent.FLAG_ACTIVITY_CLEAR_TOP or
                Intent.FLAG_ACTIVITY_SINGLE_TOP
        }
        startActivity(forward)
        finish()
    }
}
