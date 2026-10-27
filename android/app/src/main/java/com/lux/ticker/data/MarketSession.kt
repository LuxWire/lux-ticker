package com.lux.ticker.data

import java.time.DayOfWeek
import java.time.Instant
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId

object NyseCalendar {
    private val holidayCache = HashMap<Int, Set<LocalDate>>()

    fun isHoliday(date: LocalDate): Boolean = holidays(date.year).contains(date)

    fun isEarlyClose(date: LocalDate): Boolean {
        if (date.dayOfWeek == DayOfWeek.SATURDAY || date.dayOfWeek == DayOfWeek.SUNDAY) return false
        if (isHoliday(date)) return false
        val thanksgiving = nthWeekday(date.year, 11, DayOfWeek.THURSDAY, 4)
        if (date == thanksgiving.plusDays(1)) return true
        if (date.monthValue == 12 && date.dayOfMonth == 24) return true
        if (date.monthValue == 7 && date.dayOfMonth == 3) return true
        return false
    }

    fun holidays(year: Int): Set<LocalDate> = holidayCache.getOrPut(year) {
        buildSet {
            // Saturday New Year's is not observed (NYSE 2028). Sunday is observed Monday.
            newYearsClosure(year)?.let(::add)
            add(nthWeekday(year, 1, DayOfWeek.MONDAY, 3))
            add(nthWeekday(year, 2, DayOfWeek.MONDAY, 3))
            add(easterSunday(year).minusDays(2))
            add(lastWeekday(year, 5, DayOfWeek.MONDAY))
            add(observed(year, 6, 19))
            add(observed(year, 7, 4))
            add(nthWeekday(year, 9, DayOfWeek.MONDAY, 1))
            add(nthWeekday(year, 11, DayOfWeek.THURSDAY, 4))
            add(observed(year, 12, 25))
        }
    }

    private fun newYearsClosure(year: Int): LocalDate? {
        val newYear = LocalDate.of(year, 1, 1)
        return when (newYear.dayOfWeek) {
            DayOfWeek.SATURDAY -> null
            DayOfWeek.SUNDAY -> LocalDate.of(year, 1, 2)
            else -> newYear
        }
    }

    private fun observed(year: Int, month: Int, day: Int): LocalDate {
        val date = LocalDate.of(year, month, day)
        return when (date.dayOfWeek) {
            DayOfWeek.SATURDAY -> date.minusDays(1)
            DayOfWeek.SUNDAY -> date.plusDays(1)
            else -> date
        }
    }

    private fun nthWeekday(year: Int, month: Int, weekday: DayOfWeek, n: Int): LocalDate {
        var date = LocalDate.of(year, month, 1)
        var seen = 0
        while (true) {
            if (date.dayOfWeek == weekday) {
                seen++
                if (seen == n) return date
            }
            date = date.plusDays(1)
        }
    }

    private fun lastWeekday(year: Int, month: Int, weekday: DayOfWeek): LocalDate {
        var date = LocalDate.of(year, month, 1).plusMonths(1).minusDays(1)
        while (date.dayOfWeek != weekday) date = date.minusDays(1)
        return date
    }

    private fun easterSunday(year: Int): LocalDate {
        val a = year % 19
        val b = year / 100
        val c = year % 100
        val d = b / 4
        val e = b % 4
        val f = (b + 8) / 25
        val g = (b - f + 1) / 3
        val h = (19 * a + b - d - g + 15) % 30
        val i = c / 4
        val k = c % 4
        val l = (32 + 2 * e + 2 * i - h - k) % 7
        val m = (a + 11 * h + 22 * l) / 451
        val month = (h + l - 7 * m + 114) / 31
        val day = ((h + l - 7 * m + 114) % 31) + 1
        return LocalDate.of(year, month, day)
    }
}

object MarketSession {
    val NEW_YORK: ZoneId = ZoneId.of("America/New_York")
    private val preStart: LocalTime = LocalTime.of(4, 0)
    private val open: LocalTime = LocalTime.of(9, 30)
    private val regularClose: LocalTime = LocalTime.of(16, 0)
    private val earlyClose: LocalTime = LocalTime.of(13, 0)

    fun tag(symbol: String, instant: Instant, zone: ZoneId = NEW_YORK): SessionTag {
        if (SymbolRules.isCrypto(symbol) || SymbolRules.isNonUs(symbol)) return SessionTag.NONE
        val zoned = instant.atZone(zone)
        val date = zoned.toLocalDate()
        val time = zoned.toLocalTime()
        val weekend = date.dayOfWeek == DayOfWeek.SATURDAY || date.dayOfWeek == DayOfWeek.SUNDAY
        if (weekend || NyseCalendar.isHoliday(date)) return SessionTag.AFTER
        val close = if (NyseCalendar.isEarlyClose(date)) earlyClose else regularClose
        return when {
            time >= open && time < close -> SessionTag.REGULAR
            time >= preStart && time < open -> SessionTag.PRE
            else -> SessionTag.AFTER
        }
    }
}
