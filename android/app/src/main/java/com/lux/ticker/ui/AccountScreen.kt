package com.lux.ticker.ui

import android.app.Application
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import androidx.lifecycle.viewmodel.compose.viewModel
import com.lux.ticker.BuildConfig
import com.lux.ticker.LuxApp
import com.lux.ticker.account.AccountBrowser
import com.lux.ticker.account.AccountSnapshot
import kotlinx.coroutines.launch

class AccountViewModel(app: Application) : AndroidViewModel(app) {
    private val account = LuxApp.container(app).account
    val snapshot = account.snapshot

    fun signUp(email: String, password: String, username: String) = launch { account.signUp(email, password, username) }
    fun signIn(email: String, password: String) = launch { account.signIn(email, password) }
    fun signInWithUsername(username: String, password: String) = launch { account.signInWithUsername(username, password) }
    fun forgot(email: String) = launch { account.forgot(email) }
    fun signOut() = launch { account.signOut() }
    fun deleteAccount() = launch { account.deleteAccount() }
    fun saveUsername(username: String) = launch { account.saveUsername(username) }

    fun beginOAuth(provider: String) {
        val url = account.beginOAuth(provider) ?: return
        AccountBrowser.open(getApplication(), url)
    }

    private fun launch(block: suspend () -> Unit) {
        viewModelScope.launch { block() }
    }
}

@Composable
fun AccountRoute(onBack: () -> Unit) {
    val viewModel: AccountViewModel = viewModel()
    val snapshot by viewModel.snapshot.collectAsStateWithLifecycle()
    var email by rememberSaveable { mutableStateOf("") }
    var password by rememberSaveable { mutableStateOf("") }
    var username by rememberSaveable { mutableStateOf("") }
    var confirmDelete by rememberSaveable { mutableStateOf(false) }
    LaunchedEffect(snapshot.signedIn, snapshot.username) {
        if (snapshot.signedIn && username.isEmpty() && snapshot.username.isNotEmpty()) {
            username = snapshot.username
        }
    }
    if (confirmDelete) {
        AlertDialog(
            onDismissRequest = { confirmDelete = false },
            title = { Text("Delete this account?") },
            text = { Text("It is removed on this phone and on the server. The ticker stays on this phone.") },
            confirmButton = {
                TextButton(onClick = {
                    confirmDelete = false
                    viewModel.deleteAccount()
                }) { Text("Delete") }
            },
            dismissButton = { TextButton(onClick = { confirmDelete = false }) { Text("Cancel") } },
        )
    }
    SettingsPage(title = "Account", onBack = onBack) {
        Text(
            "The ticker works signed out. Sign in to sync tickers and settings. Tier: ${snapshot.tier.label()}.",
            style = MaterialTheme.typography.bodyMedium,
        )
        Spacer(Modifier.height(12.dp))
        if (snapshot.signedIn) {
            SignedInAccount(
                snapshot = snapshot,
                username = username,
                onUsername = { username = it },
                onSaveUsername = { viewModel.saveUsername(username) },
                onSignOut = { viewModel.signOut() },
                onDelete = { confirmDelete = true },
            )
        } else {
            SignedOutAccount(
                email = email,
                password = password,
                username = username,
                busy = snapshot.busy,
                onEmail = { email = it },
                onPassword = { password = it },
                onUsername = { username = it },
                onSignUp = { viewModel.signUp(email, password, username) },
                onSignIn = { viewModel.signIn(email, password) },
                onUsernameSignIn = { viewModel.signInWithUsername(username, password) },
                onForgot = { viewModel.forgot(email) },
                onOAuth = { viewModel.beginOAuth(it) },
            )
        }
        if (snapshot.busy) {
            Text("Working…", modifier = Modifier.padding(top = 12.dp))
        }
        if (snapshot.message.isNotBlank()) {
            Text(
                snapshot.message,
                color = if (snapshot.error) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onBackground,
                style = MaterialTheme.typography.bodyMedium,
                modifier = Modifier.padding(top = 12.dp),
            )
        }
    }
}

@Composable
private fun SignedOutAccount(
    email: String,
    password: String,
    username: String,
    busy: Boolean,
    onEmail: (String) -> Unit,
    onPassword: (String) -> Unit,
    onUsername: (String) -> Unit,
    onSignUp: () -> Unit,
    onSignIn: () -> Unit,
    onUsernameSignIn: () -> Unit,
    onForgot: () -> Unit,
    onOAuth: (String) -> Unit,
) {
    val context = LocalContext.current
    LuxOutlinedField(
        value = email,
        onValueChange = onEmail,
        label = { Text("Email") },
        enabled = !busy,
        modifier = Modifier.fillMaxWidth(),
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email),
    )
    LuxOutlinedField(
        value = password,
        onValueChange = onPassword,
        label = { Text("Password") },
        enabled = !busy,
        modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
        visualTransformation = PasswordVisualTransformation(),
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
    )
    LuxOutlinedField(
        value = username,
        onValueChange = onUsername,
        label = { Text("Username, optional") },
        enabled = !busy,
        modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
    )
    Text(
        "Email confirmation is on. After sign-up, open the email, then sign in. Username sign-in uses the same password.",
        style = MaterialTheme.typography.bodySmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = Modifier.padding(top = 8.dp),
    )
    OutlinedButton(onClick = onSignUp, enabled = !busy, modifier = Modifier.fillMaxWidth().padding(top = 12.dp)) {
        Text("Sign up")
    }
    OutlinedButton(onClick = onSignIn, enabled = !busy, modifier = Modifier.fillMaxWidth().padding(top = 8.dp)) {
        Text("Sign in")
    }
    OutlinedButton(onClick = onUsernameSignIn, enabled = !busy, modifier = Modifier.fillMaxWidth().padding(top = 8.dp)) {
        Text("Sign in with username")
    }
    TextButton(onClick = onForgot, enabled = !busy, modifier = Modifier.padding(top = 4.dp)) {
        Text("Forgot password")
    }
    if (BuildConfig.ACCOUNT_OAUTH) {
        OutlinedButton(onClick = { onOAuth("google") }, enabled = !busy, modifier = Modifier.fillMaxWidth().padding(top = 8.dp)) {
            Text("Continue with Google")
        }
        OutlinedButton(onClick = { onOAuth("x") }, enabled = !busy, modifier = Modifier.fillMaxWidth().padding(top = 8.dp)) {
            Text("Continue with X")
        }
    }
    TextButton(
        onClick = { AccountBrowser.open(context, ReleaseNotes.PRIVACY_URL) },
        modifier = Modifier.padding(top = 8.dp),
    ) { Text("Privacy") }
    TextButton(onClick = { AccountBrowser.open(context, ReleaseNotes.TERMS_URL) }) {
        Text("Terms")
    }
}

@Composable
private fun SignedInAccount(
    snapshot: AccountSnapshot,
    username: String,
    onUsername: (String) -> Unit,
    onSaveUsername: () -> Unit,
    onSignOut: () -> Unit,
    onDelete: () -> Unit,
) {
    Text(snapshot.email.ifBlank { "Signed in" }, style = MaterialTheme.typography.titleMedium)
    Text(
        "Sync is on for Signed in and Pro. Free keeps the ticker on this phone.",
        style = MaterialTheme.typography.bodySmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = Modifier.padding(top = 4.dp),
    )
    LuxOutlinedField(
        value = username,
        onValueChange = onUsername,
        label = { Text("Username") },
        enabled = !snapshot.busy,
        modifier = Modifier.fillMaxWidth().padding(top = 12.dp),
    )
    OutlinedButton(
        onClick = onSaveUsername,
        enabled = !snapshot.busy,
        modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
    ) { Text("Save username") }
    OutlinedButton(
        onClick = onSignOut,
        enabled = !snapshot.busy,
        modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
    ) { Text("Sign out") }
    OutlinedButton(
        onClick = onDelete,
        enabled = !snapshot.busy,
        modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
    ) { Text("Delete account") }
}
