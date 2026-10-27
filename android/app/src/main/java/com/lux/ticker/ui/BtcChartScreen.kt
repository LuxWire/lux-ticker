package com.lux.ticker.ui

import android.app.Application
import android.graphics.Paint
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.drag
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DatePicker
import androidx.compose.material3.DatePickerDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilterChipDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LocalContentColor
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SelectableDates
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.material3.rememberDatePickerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.nativeCanvas
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.unit.dp
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import androidx.lifecycle.viewmodel.CreationExtras
import androidx.lifecycle.viewmodel.compose.viewModel
import com.lux.ticker.LuxApp
import com.lux.ticker.R
import com.lux.ticker.data.BtcChart
import com.lux.ticker.data.BtcPoint
import com.lux.ticker.data.BtcPreset
import com.lux.ticker.data.BtcWindow
import com.lux.ticker.data.ChartMark
import com.lux.ticker.data.ChartRangeMarks
import com.lux.ticker.data.ChartSearch
import com.lux.ticker.data.ChartStyle
import com.lux.ticker.data.ChartSymbols
import com.lux.ticker.data.DefaultSymbols
import com.lux.ticker.data.PaperPalette
import com.lux.ticker.data.PriceFormat
import com.lux.ticker.data.SymbolRules
import com.lux.ticker.data.SymbolSpec
import com.lux.ticker.data.TickerSettings
import com.lux.ticker.data.YahooBar
import com.lux.ticker.data.YahooHttpException
import java.io.IOException
import com.lux.ticker.net.BtcChartClient
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter
import java.util.Locale
import kotlin.math.max
import kotlin.math.min

data class BtcChartUi(
    val symbol: String,
    val preset: BtcPreset = BtcPreset.MONTH,
    val custom: Boolean = false,
    val from: LocalDate = LocalDate.now().minusDays(30),
    val to: LocalDate = LocalDate.now(),
    val style: ChartStyle = ChartStyle.LINE,
    val loading: Boolean = true,
    val error: String? = null,
    val points: List<BtcPoint> = emptyList(),
    val bars: List<YahooBar> = emptyList(),
    val source: String = "",
    val rangeLabel: String = "",
    val interval: String = "",
    val viewHigh: ChartMark? = null,
    val viewLow: ChartMark? = null,
    val ath: ChartMark? = null,
    val atl: ChartMark? = null,
)

class BtcChartViewModel(app: Application, val symbol: String) : AndroidViewModel(app) {
    private val client = BtcChartClient(LuxApp.container(app).http)
    private val yahoo = LuxApp.container(app).yahoo
    private val zone = java.time.ZoneId.systemDefault()
    private val ui = MutableStateFlow(BtcChartUi(symbol = symbol))
    val state: StateFlow<BtcChartUi> = ui
    val searchError = MutableStateFlow<String?>(null)
    val searchBusy = MutableStateFlow(false)
    private var searchTicket = 0
    val knownSymbols = LuxApp.container(app).symbols.symbols.stateIn(
        viewModelScope,
        SharingStarted.WhileSubscribed(5_000),
        DefaultSymbols.all,
    )
    private var generation = 0
    private var lifetimeLoaded = false
    private var lifetime = ChartRangeMarks(null, null)

    init {
        viewModelScope.launch {
            while (isActive) {
                val ticket = generation
                val snapshot = ui.value
                val window = windowFor(snapshot)
                if (window == null) {
                    ui.update { it.copy(loading = false, error = BtcChart.RANGE_ERROR, points = emptyList(), bars = emptyList()) }
                } else {
                    ui.update {
                        it.copy(
                            loading = it.points.isEmpty(),
                            error = null,
                            rangeLabel = window.label,
                            interval = BtcChart.intervalFor(window.startMillis, window.endMillis),
                        )
                    }
                    val loaded = withContext(Dispatchers.IO) { runCatching { client.load(symbol, window) } }
                    if (!lifetimeLoaded) {
                        lifetime = withContext(Dispatchers.IO) {
                            runCatching { client.lifetimeMarks(symbol, System.currentTimeMillis()) }
                                .getOrDefault(ChartRangeMarks(null, null))
                        }
                        lifetimeLoaded = true
                    }
                    if (ticket == generation) {
                        loaded.onSuccess { series ->
                            ui.update {
                                it.copy(
                                    loading = false,
                                    error = null,
                                    points = series.points,
                                    bars = series.bars,
                                    source = series.source,
                                    rangeLabel = window.label,
                                    viewHigh = series.high,
                                    viewLow = series.low,
                                    ath = lifetime.high,
                                    atl = lifetime.low,
                                )
                            }
                        }.onFailure {
                            ui.update { current ->
                                current.copy(
                                    loading = false,
                                    error = BtcChart.loadError(symbol),
                                    rangeLabel = window.label,
                                    ath = lifetime.high,
                                    atl = lifetime.low,
                                )
                            }
                        }
                    }
                }
                val wait = window?.let { BtcChart.refreshMillis(it) } ?: 60_000L
                val started = System.currentTimeMillis()
                while (isActive && generation == ticket && System.currentTimeMillis() - started < wait) {
                    delay(250)
                }
            }
        }
    }

    fun select(preset: BtcPreset) {
        generation++
        ui.update { it.copy(preset = preset, custom = false, loading = true, error = null, points = emptyList(), bars = emptyList()) }
    }

    fun custom(from: LocalDate, to: LocalDate) {
        generation++
        ui.update { it.copy(custom = true, from = from, to = to, loading = true, error = null, points = emptyList(), bars = emptyList()) }
    }

    fun setStyle(style: ChartStyle) {
        ui.update { it.copy(style = style) }
    }

    fun retry() {
        generation++
    }

    fun clearSearch() {
        searchTicket++
        searchError.value = null
        searchBusy.value = false
    }

    fun submitSearch(raw: String, known: List<SymbolSpec>, onOpen: (String) -> Unit) {
        val symbol = ChartSearch.normalize(raw)
        val problem = ChartSearch.formatError(symbol)
        val ticket = ++searchTicket
        if (problem != null) {
            searchError.value = problem
            searchBusy.value = false
            return
        }
        val knownHit = ChartSymbols.isBtc(symbol) ||
            symbol.equals(this.symbol, ignoreCase = true) ||
            known.any { it.symbol.equals(symbol, ignoreCase = true) }
        if (knownHit) {
            searchError.value = null
            searchBusy.value = false
            onOpen(symbol)
            return
        }
        viewModelScope.launch {
            searchBusy.value = true
            searchError.value = null
            val result = withContext(Dispatchers.IO) { runCatching { yahoo.fetch(symbol) } }
            if (ticket != searchTicket) return@launch
            searchBusy.value = false
            result.onSuccess { onOpen(symbol) }.onFailure { error ->
                searchError.value = when (error) {
                    is IOException, is YahooHttpException -> ChartSearch.offlineMessage(symbol)
                    else -> ChartSearch.unknownMessage(symbol)
                }
            }
        }
    }

    private fun windowFor(snapshot: BtcChartUi): BtcWindow? {
        val now = Instant.now()
        return if (snapshot.custom) BtcChart.custom(snapshot.from, snapshot.to, now, zone) else BtcChart.preset(snapshot.preset, now, zone)
    }

    companion object {
        fun factory(symbol: String): ViewModelProvider.Factory = object : ViewModelProvider.Factory {
            override fun <T : ViewModel> create(modelClass: Class<T>, extras: CreationExtras): T {
                val app = checkNotNull(extras[ViewModelProvider.AndroidViewModelFactory.APPLICATION_KEY])
                @Suppress("UNCHECKED_CAST")
                return BtcChartViewModel(app, ChartSymbols.normalize(symbol)) as T
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class, ExperimentalLayoutApi::class)
@Composable
fun BtcChartRoute(symbol: String, onBack: () -> Unit, onOpenSymbol: (String) -> Unit) {
    val viewModel: BtcChartViewModel = viewModel(
        key = symbol,
        factory = BtcChartViewModel.factory(symbol),
    )
    val state by viewModel.state.collectAsStateWithLifecycle()
    val known by viewModel.knownSymbols.collectAsStateWithLifecycle()
    val searchError by viewModel.searchError.collectAsStateWithLifecycle()
    val searchBusy by viewModel.searchBusy.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val chartSettings by LuxApp.container(context).settings.settings.collectAsStateWithLifecycle(
        initialValue = TickerSettings(),
    )
    val hapticsOn by rememberUpdatedState(chartSettings.chartHaptics)
    var pickingFrom by remember { mutableStateOf(false) }
    var pickingTo by remember { mutableStateOf(false) }
    var query by remember { mutableStateOf("") }
    if (pickingFrom) {
        BtcDateDialog(
            initial = state.from,
            onDismiss = { pickingFrom = false },
            onDate = { date ->
                pickingFrom = false
                viewModel.custom(date, state.to)
            },
        )
    }
    if (pickingTo) {
        BtcDateDialog(
            initial = state.to,
            onDismiss = { pickingTo = false },
            onDate = { date ->
                pickingTo = false
                viewModel.custom(state.from, date)
            },
        )
    }
    val paper = Color(PaperPalette.BG)
    val panel = Color(PaperPalette.PANEL)
    val ink = Color(PaperPalette.INK)
    val body = Color(PaperPalette.BODY)
    val series = Color(PaperPalette.ACCENT)
    val down = Color(BtcChart.CANDLE_DOWN)
    val last = state.points.lastOrNull()?.price
    val change = BtcChart.changePct(state.points)
    val candleBars = if (state.bars.isNotEmpty()) state.bars else BtcChart.closesAsBars(state.points)
    val drawnCandles = BtcChart.drawBars(candleBars)
    val scrubSource = if (state.style == ChartStyle.CANDLES) {
        drawnCandles.map { BtcPoint(it.epochMillis, it.close) }
    } else {
        state.points
    }
    var scrubIndex by remember(scrubSource) { mutableStateOf<Int?>(null) }
    val scrubPoint = scrubIndex?.let { scrubSource.getOrNull(it) }
    val scrubBar = if (state.style == ChartStyle.CANDLES) scrubIndex?.let { drawnCandles.getOrNull(it) } else null
    val view = LocalView.current
    var lastTick by remember(scrubSource) { mutableIntStateOf(-1) }
    fun scrubTo(index: Int?) {
        scrubIndex = index
        if (BtcChart.scrubTick(lastTick, index)) {
            lastTick = index ?: lastTick
            ChartHaptics.tick(view, hapticsOn)
        }
        if (index == null) lastTick = -1
    }
    val title = ChartSymbols.title(state.symbol)
    val pair = ChartSymbols.pair(state.symbol)
    val viewName = if (state.custom) "Range" else state.preset.label
    val typed = ChartSearch.normalize(query)
    val matches = ChartSearch.suggestions(query, known)
    fun submitSearch() {
        viewModel.submitSearch(query, known, onOpenSymbol)
    }
    Scaffold(
        modifier = Modifier.windowInsetsPadding(WindowInsets.safeDrawing),
        containerColor = paper,
        topBar = {
            TopAppBar(
                title = { Text(title) },
                navigationIcon = { TextButton(onClick = onBack) { Text("Back", color = body) } },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = paper,
                    titleContentColor = body,
                    navigationIconContentColor = body,
                    actionIconContentColor = body,
                ),
            )
        },
    ) { padding ->
        CompositionLocalProvider(LocalContentColor provides body) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 16.dp, vertical = 8.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            LuxOutlinedField(
                value = query,
                onValueChange = {
                    query = it.uppercase(Locale.US)
                    viewModel.clearSearch()
                },
                modifier = Modifier.fillMaxWidth(),
                forcePaper = true,
                isError = searchError != null,
                label = { Text("Ticker") },
                supportingText = if (searchBusy || searchError != null) {
                    {
                        if (searchError != null) {
                            Text(searchError ?: "", color = Color(PaperPalette.LOSS))
                        } else {
                            Text("Looking up $typed.", color = body)
                        }
                    }
                } else {
                    null
                },
                keyboardOptions = KeyboardOptions(
                    capitalization = KeyboardCapitalization.Characters,
                    imeAction = ImeAction.Search,
                ),
                keyboardActions = KeyboardActions(
                    onSearch = { submitSearch() },
                    onDone = { submitSearch() },
                ),
                trailingIcon = {
                    IconButton(
                        onClick = { submitSearch() },
                        enabled = !searchBusy,
                        modifier = Modifier.size(48.dp),
                    ) {
                        Icon(
                            painter = painterResource(R.drawable.ic_search),
                            contentDescription = "Search",
                            tint = body,
                        )
                    }
                },
            )
            matches.forEach { spec ->
                if (!spec.symbol.equals(state.symbol, ignoreCase = true)) {
                    TextButton(onClick = { onOpenSymbol(spec.symbol) }) { Text(spec.label, color = body) }
                }
            }
            if (
                SymbolRules.isValid(typed) &&
                !typed.equals(state.symbol, ignoreCase = true) &&
                matches.none { it.symbol.equals(typed, ignoreCase = true) }
            ) {
                TextButton(onClick = { submitSearch() }) { Text("Open $typed", color = body) }
            }
            Text(pair, style = MaterialTheme.typography.titleMedium, color = ink)
            FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                BtcPreset.entries.forEach { preset ->
                    FilterChip(
                        selected = !state.custom && state.preset == preset,
                        onClick = { viewModel.select(preset) },
                        label = { Text(preset.label) },
                        modifier = Modifier.heightIn(min = 48.dp),
                        colors = paperChipColors(panel, ink, series),
                    )
                }
                FilterChip(
                    selected = state.custom,
                    onClick = { viewModel.custom(state.from, state.to) },
                    label = { Text("Custom") },
                    modifier = Modifier.heightIn(min = 48.dp),
                    colors = paperChipColors(panel, ink, series),
                )
            }
            FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                FilterChip(
                    selected = state.style == ChartStyle.LINE,
                    onClick = { viewModel.setStyle(ChartStyle.LINE) },
                    label = { Text("Line") },
                    modifier = Modifier.heightIn(min = 48.dp),
                    colors = paperChipColors(panel, ink, series),
                )
                FilterChip(
                    selected = state.style == ChartStyle.CANDLES,
                    onClick = { viewModel.setStyle(ChartStyle.CANDLES) },
                    label = { Text("Candles") },
                    modifier = Modifier.heightIn(min = 48.dp),
                    colors = paperChipColors(panel, ink, series),
                )
            }
            if (state.style == ChartStyle.CANDLES && state.interval.isNotBlank()) {
                Text(BtcChart.intervalCaption(state.interval), color = ink, style = MaterialTheme.typography.bodyMedium)
            }
            if (state.custom) {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                    Button(
                        onClick = { pickingFrom = true },
                        modifier = Modifier.weight(1f).heightIn(min = 48.dp),
                    ) { Text("From ${shortDate(state.from)}") }
                    Button(
                        onClick = { pickingTo = true },
                        modifier = Modifier.weight(1f).heightIn(min = 48.dp),
                    ) { Text("To ${shortDate(state.to)}") }
                }
            }
            if (last != null || scrubPoint != null || scrubBar != null) {
                if (scrubBar != null) {
                    Text("O ${money(state.symbol, scrubBar.open)}    H ${money(state.symbol, scrubBar.high)}", color = body, style = MaterialTheme.typography.titleMedium)
                    Text("L ${money(state.symbol, scrubBar.low)}    C ${money(state.symbol, scrubBar.close)}", color = body, style = MaterialTheme.typography.titleMedium)
                    Text(BtcChart.formatEt(scrubBar.epochMillis), color = ink, style = MaterialTheme.typography.titleMedium)
                } else {
                    val shown = scrubPoint?.price ?: last
                    if (shown != null) {
                        Text(money(state.symbol, shown), style = MaterialTheme.typography.headlineMedium, color = body)
                    }
                    if (scrubPoint != null) {
                        Text(BtcChart.formatEt(scrubPoint.epochMillis), color = ink, style = MaterialTheme.typography.titleMedium)
                    } else if (change != null) {
                        val tone = if (change >= 0.0) Color(PaperPalette.GAIN) else Color(PaperPalette.LOSS)
                        Text(
                            PriceFormat.formatPct(change) + " in this range",
                            color = tone,
                            style = MaterialTheme.typography.titleMedium,
                        )
                    }
                }
            }
            when {
                state.loading && state.points.isEmpty() -> {
                    CircularProgressIndicator(color = series)
                    Text("Loading $title prices.")
                }
                state.points.size < 2 && state.error != null -> {
                    Text(state.error ?: BtcChart.loadError(state.symbol), color = Color(PaperPalette.LOSS))
                    Button(onClick = viewModel::retry, modifier = Modifier.heightIn(min = 48.dp)) { Text("Try again") }
                }
                state.points.size < 2 -> Text("No prices in that range.")
                else -> {
                    if (state.style == ChartStyle.CANDLES) {
                        CandleChart(
                            bars = drawnCandles,
                            paper = paper,
                            ink = ink,
                            up = series,
                            down = down,
                            selected = scrubBar,
                            onScrub = ::scrubTo,
                            description = chartDescription(pair, scrubBar?.close ?: last, change),
                        )
                    } else {
                        BtcLineChart(
                            points = state.points,
                            paper = paper,
                            ink = ink,
                            series = series,
                            selected = scrubPoint,
                            onScrub = ::scrubTo,
                            description = chartDescription(pair, scrubPoint?.price ?: last, change),
                        )
                    }
                    Text(state.rangeLabel, style = MaterialTheme.typography.bodyMedium, color = ink)
                    MarkLine("$viewName high", state.viewHigh, state.symbol, ink, body)
                    MarkLine("$viewName low", state.viewLow, state.symbol, ink, body)
                    MarkLine("All-time high", state.ath, state.symbol, ink, body)
                    MarkLine("All-time low", state.atl, state.symbol, ink, body)
                    if (state.source.isNotBlank()) {
                        Text(
                            state.source,
                            style = MaterialTheme.typography.bodySmall,
                            color = Color(PaperPalette.SUB),
                        )
                    }
                    if (state.error != null) {
                        Text(state.error ?: "", color = Color(PaperPalette.LOSS))
                        Button(onClick = viewModel::retry, modifier = Modifier.heightIn(min = 48.dp)) { Text("Try again") }
                    }
                }
            }
            Spacer(Modifier.height(12.dp))
        }
        }
    }
}

@Composable
private fun MarkLine(label: String, mark: ChartMark?, symbol: String, ink: Color, body: Color) {
    if (mark == null) return
    Text(label, color = ink, style = MaterialTheme.typography.titleSmall)
    Text(
        money(symbol, mark.price) + "  ·  " + BtcChart.formatEt(mark.epochMillis),
        color = body,
        style = MaterialTheme.typography.bodyMedium,
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun BtcDateDialog(initial: LocalDate, onDismiss: () -> Unit, onDate: (LocalDate) -> Unit) {
    val state = rememberDatePickerState(
        initialSelectedDateMillis = initial.atStartOfDay(ZoneOffset.UTC).toInstant().toEpochMilli(),
        yearRange = BtcChart.EARLIEST.year..LocalDate.now().year,
        selectableDates = BtcSelectableDates,
    )
    DatePickerDialog(
        onDismissRequest = onDismiss,
        confirmButton = {
            TextButton(onClick = {
                val millis = state.selectedDateMillis
                if (millis != null) onDate(Instant.ofEpochMilli(millis).atZone(ZoneOffset.UTC).toLocalDate())
                else onDismiss()
            }) { Text("OK") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } },
    ) {
        DatePicker(state = state)
    }
}

private object BtcSelectableDates : SelectableDates {
    override fun isSelectableDate(utcTimeMillis: Long): Boolean {
        val date = Instant.ofEpochMilli(utcTimeMillis).atZone(ZoneOffset.UTC).toLocalDate()
        val latest = LocalDate.now().plusDays(1)
        return !date.isBefore(BtcChart.EARLIEST) && !date.isAfter(latest)
    }
}

@Composable
internal fun BtcLineChart(
    points: List<BtcPoint>,
    paper: Color,
    ink: Color,
    series: Color,
    selected: BtcPoint?,
    onScrub: (Int?) -> Unit,
    description: String,
) {
    val drawn = BtcChart.drawPoints(points)
    ChartCanvas(points.map { it.epochMillis to it.price }, paper, ink, description, onScrub, points) { _, _, _, x, y, top, bottom, _, _ ->
        val line = Path()
        drawn.forEachIndexed { index, point ->
            val px = x(point.epochMillis)
            val py = y(point.price)
            if (index == 0) line.moveTo(px, py) else line.lineTo(px, py)
        }
        val fill = Path().apply {
            addPath(line)
            lineTo(x(drawn.last().epochMillis), bottom)
            lineTo(x(drawn.first().epochMillis), bottom)
            close()
        }
        drawPath(fill, series.copy(alpha = 0.16f))
        drawPath(line, series, style = androidx.compose.ui.graphics.drawscope.Stroke(width = 3.dp.toPx()))
        selected?.let { point ->
            val sx = x(point.epochMillis)
            val sy = y(point.price)
            drawLine(ink, Offset(sx, top), Offset(sx, bottom), strokeWidth = 1.5.dp.toPx())
            drawCircle(series, radius = 5.dp.toPx(), center = Offset(sx, sy))
            drawCircle(paper, radius = 2.5.dp.toPx(), center = Offset(sx, sy))
        }
    }
}

@Composable
internal fun CandleChart(
    bars: List<YahooBar>,
    paper: Color,
    ink: Color,
    up: Color,
    down: Color,
    selected: YahooBar?,
    onScrub: (Int?) -> Unit,
    description: String,
) {
    val points = bars.map { BtcPoint(it.epochMillis, it.close) }
    ChartCanvas(bars.map { it.epochMillis to it.high }, paper, ink, description, onScrub, points, lows = bars.map { it.low }) { _, _, _, x, y, top, bottom, _, _ ->
        if (bars.size < 2) return@ChartCanvas
        val slot = ((size.width - 16.dp.toPx()) / bars.size.toFloat()).coerceAtLeast(2.dp.toPx())
        val half = (slot * 0.32f).coerceIn(1.dp.toPx(), 7.dp.toPx())
        bars.forEach { bar ->
            val cx = x(bar.epochMillis)
            val rising = bar.close >= bar.open
            val color = if (rising) up else down
            drawLine(color, Offset(cx, y(bar.high)), Offset(cx, y(bar.low)), strokeWidth = 1.5.dp.toPx())
            val bodyTop = y(max(bar.open, bar.close))
            val bodyBottom = y(min(bar.open, bar.close))
            drawRect(
                color,
                topLeft = Offset(cx - half, bodyTop),
                size = Size(half * 2f, (bodyBottom - bodyTop).coerceAtLeast(1.5.dp.toPx())),
            )
        }
        selected?.let { bar ->
            val sx = x(bar.epochMillis)
            drawLine(ink, Offset(sx, top), Offset(sx, bottom), strokeWidth = 1.5.dp.toPx())
        }
    }
}

@Composable
private fun ChartCanvas(
    samples: List<Pair<Long, Double>>,
    paper: Color,
    ink: Color,
    description: String,
    onScrub: (Int?) -> Unit,
    scrubPoints: List<BtcPoint>,
    lows: List<Double> = emptyList(),
    drawSeries: androidx.compose.ui.graphics.drawscope.DrawScope.(
        minPrice: Double,
        maxPrice: Double,
        spanPrice: Double,
        x: (Long) -> Float,
        y: (Double) -> Float,
        top: Float,
        bottom: Float,
        left: Float,
        right: Float,
    ) -> Unit,
) {
    Canvas(
        modifier = Modifier
            .fillMaxWidth()
            .height(280.dp)
            .semantics { contentDescription = description }
            .pointerInput(scrubPoints) {
                val pad = 8.dp.toPx()
                awaitEachGesture {
                    val down = awaitFirstDown()
                    down.consume()
                    fun indexAt(x: Float): Int {
                        val span = (size.width - pad * 2f).coerceAtLeast(1f)
                        val fraction = ((x - pad) / span).coerceIn(0f, 1f)
                        return BtcChart.nearestIndex(scrubPoints, fraction)
                    }
                    onScrub(indexAt(down.position.x))
                    drag(down.id) { change ->
                        onScrub(indexAt(change.position.x))
                        change.consume()
                    }
                    onScrub(null)
                }
            },
    ) {
        drawRect(paper)
        if (samples.size < 2) return@Canvas
        val minPrice = minOf(samples.minOf { it.second }, lows.minOrNull() ?: samples.minOf { it.second })
        val maxPrice = samples.maxOf { it.second }
        val spanPrice = (maxPrice - minPrice).takeIf { it > 0.0 } ?: (maxPrice * 0.02).coerceAtLeast(1.0)
        val minTime = samples.first().first.toDouble()
        val spanTime = (samples.last().first - samples.first().first).toDouble().coerceAtLeast(1.0)
        val left = 8.dp.toPx()
        val right = size.width - 8.dp.toPx()
        val top = 28.dp.toPx()
        val bottom = size.height - 36.dp.toPx()
        fun x(millis: Long) = left + ((millis - minTime) / spanTime).toFloat() * (right - left)
        fun y(price: Double) = top + ((maxPrice - price) / spanPrice).toFloat() * (bottom - top)
        drawSeries(minPrice, maxPrice, spanPrice, ::x, ::y, top, bottom, left, right)
        val label = Paint().apply {
            color = ink.toArgb()
            textSize = 12.dp.toPx()
            isAntiAlias = true
        }
        val canvas = drawContext.canvas.nativeCanvas
        canvas.drawText(priceTick(maxPrice), left, 16.dp.toPx(), label)
        canvas.drawText(priceTick(minPrice), left, bottom - 4.dp.toPx(), label)
        label.textAlign = Paint.Align.RIGHT
        canvas.drawText(axisTime(samples.last().first, spanTime), right, size.height - 8.dp.toPx(), label)
        label.textAlign = Paint.Align.LEFT
        canvas.drawText(axisTime(samples.first().first, spanTime), left, size.height - 8.dp.toPx(), label)
        drawLine(ink.copy(alpha = 0.25f), Offset(left, top), Offset(right, top), strokeWidth = 1.dp.toPx())
        drawLine(ink.copy(alpha = 0.25f), Offset(left, bottom), Offset(right, bottom), strokeWidth = 1.dp.toPx())
    }
}

private fun priceTick(price: Double): String = "$" + PriceFormat.formatPrice(price, "USD")

private fun money(symbol: String, price: Double): String {
    val currency = when {
        symbol.endsWith(".T") -> "JPY"
        symbol.endsWith(".L") -> "GBP"
        else -> "USD"
    }
    val formatted = PriceFormat.formatPrice(price, currency)
    return if (currency == "USD") "$$formatted" else formatted
}

private fun axisTime(millis: Long, spanMillis: Double): String {
    val zone = java.time.ZoneId.of("America/New_York")
    val pattern = if (spanMillis <= 2 * 86_400_000.0) "MMM d HH:mm" else "MMM d, yyyy"
    return DateTimeFormatter.ofPattern(pattern, Locale.US).withZone(zone).format(Instant.ofEpochMilli(millis))
}

private fun shortDate(date: LocalDate): String =
    DateTimeFormatter.ofPattern("MMM d, yyyy", Locale.US).format(date)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun paperChipColors(panel: Color, ink: Color, series: Color) = FilterChipDefaults.filterChipColors(
    containerColor = panel,
    labelColor = ink,
    iconColor = ink,
    selectedContainerColor = series,
    selectedLabelColor = Color.White,
    selectedLeadingIconColor = Color.White,
)

private fun chartDescription(pair: String, last: Double?, change: Double?): String {
    val price = last?.let { "$" + PriceFormat.formatPrice(it, "USD") } ?: "no price"
    val move = change?.let { PriceFormat.formatPct(it) } ?: "no change"
    return "$pair chart, last $price, $move in this range"
}
