package com.lux.ticker.luxwire

import android.app.Activity
import android.os.Bundle
import android.widget.Toast
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/** Receives the X redirect `luxwireticker://oauth` and stores the user token. */
class LuxWireOAuthActivity : Activity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val uri = intent?.data
        val error = uri?.getQueryParameter("error")
        val code = uri?.getQueryParameter("code").orEmpty()
        val state = uri?.getQueryParameter("state").orEmpty()
        if (!error.isNullOrBlank() || code.isBlank()) {
            toast(if (error.isNullOrBlank()) "X sign-in did not return a code" else "X sign-in was cancelled")
            finish()
            return
        }
        val app = applicationContext
        CoroutineScope(Dispatchers.IO).launch {
            val message = runCatching {
                LuxWireAuth(app).exchange(code, state)
                "Signed in to X"
            }.getOrElse { "X sign-in failed" }
            withContext(Dispatchers.Main) {
                if (!isFinishing) {
                    Toast.makeText(app, message, Toast.LENGTH_LONG).show()
                    finish()
                }
            }
        }
    }

    private fun toast(text: String) {
        Toast.makeText(applicationContext, text, Toast.LENGTH_LONG).show()
    }
}
