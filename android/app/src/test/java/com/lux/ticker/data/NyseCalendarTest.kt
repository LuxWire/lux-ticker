package com.lux.ticker.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.LocalDate

class NyseCalendarTest {
    @Test fun officialClosures2026() {
        assertHoliday(2026, 1, 1)
        assertHoliday(2026, 1, 19)
        assertHoliday(2026, 2, 16)
        assertHoliday(2026, 4, 3)
        assertHoliday(2026, 5, 25)
        assertHoliday(2026, 6, 19)
        assertHoliday(2026, 7, 3)
        assertHoliday(2026, 9, 7)
        assertHoliday(2026, 11, 26)
        assertHoliday(2026, 12, 25)
        assertFalse(NyseCalendar.isHoliday(LocalDate.of(2026, 10, 22)))
    }

    @Test fun observedAndEarlyClosesMatchThePublishedCalendar() {
        assertHoliday(2025, 7, 4)
        assertEarly(2025, 7, 3)
        assertEarly(2025, 11, 28)
        assertEarly(2025, 12, 24)

        assertFalse(NyseCalendar.isEarlyClose(LocalDate.of(2026, 7, 3)))
        assertEarly(2026, 11, 27)
        assertEarly(2026, 12, 24)

        assertHoliday(2027, 6, 18)
        assertHoliday(2027, 7, 5)
        assertHoliday(2027, 12, 24)
        assertFalse(NyseCalendar.isEarlyClose(LocalDate.of(2027, 12, 24)))
        assertFalse(NyseCalendar.isHoliday(LocalDate.of(2027, 12, 31)))

        assertEarly(2028, 7, 3)
        assertFalse(NyseCalendar.isHoliday(LocalDate.of(2028, 1, 1)))
        assertHoliday(2028, 4, 14)
    }

    @Test fun goodFridayAndWeekendRules() {
        assertHoliday(2024, 3, 29)
        assertHoliday(2025, 4, 18)
        assertHoliday(2027, 3, 26)
        assertFalse(NyseCalendar.isEarlyClose(LocalDate.of(2026, 10, 24)))
        assertFalse(NyseCalendar.isHoliday(LocalDate.of(2026, 10, 24)))
    }

    private fun assertHoliday(year: Int, month: Int, day: Int) {
        assertTrue("$year-$month-$day", NyseCalendar.isHoliday(LocalDate.of(year, month, day)))
    }

    private fun assertEarly(year: Int, month: Int, day: Int) {
        val date = LocalDate.of(year, month, day)
        assertTrue("$date early", NyseCalendar.isEarlyClose(date))
        assertFalse("$date not a full close", NyseCalendar.isHoliday(date))
    }

    @Test fun holidayCountDoesNotDoubleCount() {
        assertEquals(10, NyseCalendar.holidays(2026).size)
        assertEquals(10, NyseCalendar.holidays(2025).size)
    }
}
