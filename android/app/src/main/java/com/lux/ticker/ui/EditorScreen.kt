package com.lux.ticker.ui

import android.app.Application
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectDragGesturesAfterLongPress
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.unit.dp
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import androidx.lifecycle.viewmodel.compose.viewModel
import com.lux.ticker.LuxApp
import com.lux.ticker.data.DefaultSymbols
import com.lux.ticker.data.PriceFormat
import com.lux.ticker.data.SymbolList
import com.lux.ticker.data.SymbolRules
import com.lux.ticker.data.SymbolSpec
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

class EditorViewModel(app: Application) : AndroidViewModel(app) {
    private val container = LuxApp.container(app)
    val symbols = container.symbols.symbols.stateIn(
        viewModelScope,
        SharingStarted.WhileSubscribed(5_000),
        DefaultSymbols.all,
    )
    val quotes = container.quotes.quotes
    private val messageState = MutableStateFlow<String?>(null)
    val message = messageState.asStateFlow()
    private val busyState = MutableStateFlow(false)
    val busy = busyState.asStateFlow()

    fun moveBy(symbol: String, delta: Int) {
        if (symbol == SymbolList.PINNED) return
        viewModelScope.launch {
            val current = container.symbols.read()
            val scroll = SymbolList.scrolling(current)
            val from = scroll.indexOfFirst { it.symbol == symbol }
            val to = from + delta
            if (from < 0 || to !in scroll.indices) return@launch
            container.symbols.update(SymbolList.moveScrolling(current, from, to))
        }
    }

    fun remove(symbol: String) {
        if (symbol == SymbolList.PINNED) return
        viewModelScope.launch {
            container.symbols.update(SymbolList.remove(container.symbols.read(), symbol))
        }
    }

    fun reset() {
        viewModelScope.launch {
            container.symbols.update(DefaultSymbols.all)
            messageState.value = null
        }
    }

    fun add(raw: String) {
        val symbol = SymbolRules.normalize(raw)
        if (symbol == SymbolList.PINNED) {
            messageState.value = "BTC stays on the tape."
            return
        }
        if (!SymbolRules.isValid(symbol)) {
            messageState.value = ReleaseNotes.INVALID_TICKER
            return
        }
        viewModelScope.launch {
            val current = container.symbols.read()
            if (current.any { it.symbol == symbol }) {
                messageState.value = "$symbol is already on the tape."
                return@launch
            }
            busyState.value = true
            messageState.value = null
            val result = runCatching { container.yahoo.fetch(symbol) }
            busyState.value = false
            result.onSuccess {
                val label = DefaultSymbols.labels[symbol] ?: symbol
                container.symbols.update(SymbolList.add(container.symbols.read(), SymbolSpec(symbol, label)))
            }.onFailure {
                messageState.value = if (com.lux.ticker.data.PriceAlerts.badSymbol(it)) {
                    ReleaseNotes.INVALID_TICKER
                } else {
                    ReleaseNotes.REFRESH_ERROR
                }
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun EditorRoute(onBack: () -> Unit, onChart: (String) -> Unit) {
    val viewModel: EditorViewModel = viewModel()
    val symbols by viewModel.symbols.collectAsStateWithLifecycle()
    val quotes by viewModel.quotes.collectAsStateWithLifecycle()
    val message by viewModel.message.collectAsStateWithLifecycle()
    val busy by viewModel.busy.collectAsStateWithLifecycle()
    var draft by remember { mutableStateOf("") }
    var confirmReset by remember { mutableStateOf(false) }
    var dragging by remember { mutableStateOf(false) }
    val scrolling = symbols.drop(1)
    if (confirmReset) {
        AlertDialog(
            onDismissRequest = { confirmReset = false },
            title = { Text("Reset the tape?") },
            text = { Text("This puts back the original list. Bitcoin stays on the tape.") },
            confirmButton = {
                TextButton(onClick = {
                    confirmReset = false
                    viewModel.reset()
                }) { Text("Reset") }
            },
            dismissButton = { TextButton(onClick = { confirmReset = false }) { Text("Cancel") } },
        )
    }
    Scaffold(
        modifier = Modifier.windowInsetsPadding(WindowInsets.safeDrawing),
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            TopAppBar(
                title = { Text("Tickers") },
                navigationIcon = { TextButton(onClick = onBack) { Text("Back") } },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.background,
                    titleContentColor = MaterialTheme.colorScheme.onBackground,
                    navigationIconContentColor = MaterialTheme.colorScheme.onBackground,
                ),
            )
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(horizontal = 16.dp),
        ) {
            Card(
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant),
                onClick = { onChart("BTC") },
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(16.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Column(Modifier.weight(1f)) {
                        Text(
                            "BTC",
                            color = MaterialTheme.colorScheme.secondary,
                            fontFamily = TapeFont,
                            fontWeight = FontWeight.Bold,
                            style = MaterialTheme.typography.titleMedium,
                        )
                        Text(
                            "On the tape. Pin is set in Settings.",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                    val btc = quotes["BTC"]
                    Text(
                        if (btc == null) "—" else PriceFormat.formatPrice(btc.price, btc.currency),
                        fontFamily = TapeFont,
                    )
                }
            }
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 12.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                LuxOutlinedField(
                    value = draft,
                    onValueChange = { draft = it.uppercase() },
                    modifier = Modifier.weight(1f),
                    singleLine = true,
                    label = { Text("Add ticker") },
                    keyboardOptions = KeyboardOptions(imeAction = ImeAction.Done),
                    keyboardActions = KeyboardActions(onDone = {
                        viewModel.add(draft)
                        draft = ""
                    }),
                )
                Button(
                    onClick = {
                        viewModel.add(draft)
                        draft = ""
                    },
                    enabled = draft.isNotBlank() && !busy,
                    modifier = Modifier.padding(start = 8.dp),
                ) { Text(if (busy) "…" else "Add ticker") }
            }
            if (message != null) {
                Text(
                    message!!,
                    color = MaterialTheme.colorScheme.error,
                    modifier = Modifier.padding(top = 8.dp),
                )
            }
            if (scrolling.isEmpty()) {
                Text(
                    ReleaseNotes.EMPTY_TICKERS,
                    modifier = Modifier.padding(top = 12.dp),
                )
            }
            Text(
                "Hold ≡ and drag to reorder. Scrolling names only.",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 12.dp, bottom = 4.dp),
            )
            LazyColumn(
                modifier = Modifier.weight(1f),
                userScrollEnabled = !dragging,
            ) {
                itemsIndexed(scrolling, key = { _, spec -> spec.symbol }) { _, spec ->
                    val quote = quotes[spec.symbol]
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(vertical = 2.dp)
                            .background(MaterialTheme.colorScheme.surface)
                            .padding(horizontal = 4.dp, vertical = 6.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Text(
                            "≡",
                            modifier = Modifier
                                .pointerInput(spec.symbol) {
                                    var accum = 0f
                                    detectDragGesturesAfterLongPress(
                                        onDragStart = { dragging = true; accum = 0f },
                                        onDragEnd = { dragging = false },
                                        onDragCancel = { dragging = false },
                                        onDrag = { change, drag ->
                                            change.consume()
                                            accum += drag.y
                                            val row = 48.dp.toPx()
                                            if (accum > row) {
                                                accum = 0f
                                                viewModel.moveBy(spec.symbol, 1)
                                            } else if (accum < -row) {
                                                accum = 0f
                                                viewModel.moveBy(spec.symbol, -1)
                                            }
                                        },
                                    )
                                }
                                .padding(12.dp),
                            fontFamily = TapeFont,
                            color = MaterialTheme.colorScheme.primary,
                        )
                        Column(
                            Modifier
                                .weight(1f)
                                .clickable { onChart(spec.symbol) },
                        ) {
                            Text(spec.label, fontFamily = TapeFont, fontWeight = FontWeight.Bold)
                            if (quote != null) {
                                Text(
                                    PriceFormat.formatPrice(quote.price, quote.currency),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                            }
                        }
                        TextButton(onClick = { viewModel.moveBy(spec.symbol, -1) }) { Text("Up") }
                        TextButton(onClick = { viewModel.moveBy(spec.symbol, 1) }) { Text("Down") }
                        TextButton(onClick = { viewModel.remove(spec.symbol) }) { Text("Remove ticker") }
                    }
                }
            }
            TextButton(
                onClick = { confirmReset = true },
                modifier = Modifier.align(Alignment.End),
            ) { Text("Reset to default") }
        }
    }
}
