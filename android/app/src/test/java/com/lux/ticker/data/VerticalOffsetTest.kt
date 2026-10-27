package com.lux.ticker.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class VerticalOffsetTest {
    @Test fun typedValuesClampToTenthsAndUnfinishedTextStaysNull() {
        assertEquals(30.5f, VerticalOffset.parse("30.5")!!, 0.001f)
        assertEquals(30.5f, VerticalOffset.parse("30.54")!!, 0.001f)
        assertEquals(12.3f, VerticalOffset.parse("12.25")!!, 0.001f)
        assertEquals(800f, VerticalOffset.parse("999")!!, 0.001f)
        assertEquals(-120f, VerticalOffset.parse("-200")!!, 0.001f)
        assertEquals(-4.2f, VerticalOffset.parse("-4.2")!!, 0.001f)
        assertNull(VerticalOffset.parse("not a number"))
        assertNull(VerticalOffset.parse(""))
        assertNull(VerticalOffset.parse("-"))
        assertNull(VerticalOffset.parse("30."))
        assertEquals("30.5", VerticalOffset.format(30.5f))
        assertEquals("-4.2", VerticalOffset.format(-4.2f))
        assertEquals("800.0", VerticalOffset.format(999f))
        assertEquals("0.0", VerticalOffset.format(Float.NaN))
        assertEquals(0f, VerticalOffset.DEFAULT_DP)
    }
}
