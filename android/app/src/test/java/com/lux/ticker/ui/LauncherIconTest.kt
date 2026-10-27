package com.lux.ticker.ui

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File
import java.util.zip.Inflater
import kotlin.math.hypot

class LauncherIconTest {
    @Test fun adaptiveForegroundIsAnOrangePlateWithPaperHoles() {
        val image = readPng(resource("mipmap-xxxhdpi/ic_launcher_foreground.png"))
        assertEquals(432, image.width)
        assertEquals(432, image.height)
        val safeRadius = (66.0 / 108.0) * image.width / 2.0
        val cx = (image.width - 1) / 2.0
        val cy = (image.height - 1) / 2.0
        var holes = 0
        var holesOutside = 0
        assertEquals(0xFFF7931A.toInt(), image.pixel(0, 0))
        assertEquals(0, image.pixel(240, 140) ushr 24)
        for (y in 0 until image.height) {
            for (x in 0 until image.width) {
                val pixel = image.pixel(x, y)
                val alpha = pixel ushr 24 and 0xFF
                if (alpha == 0) {
                    holes++
                    if (hypot(x - cx, y - cy) > safeRadius + 1.5) holesOutside++
                } else {
                    assertEquals(0xFFF7931A.toInt(), pixel)
                }
            }
        }
        assertTrue("letter holes should show through, holes=$holes", holes > 1000)
        assertEquals(0, holesOutside)
    }

    @Test fun legacyMipmapsUseSampledOrangeAndTheNotificationIsWhite() {
        val legacy = readPng(resource("mipmap-xxxhdpi/ic_launcher.png"))
        assertEquals(192, legacy.width)
        assertEquals(0xFFF7931A.toInt(), legacy.pixel(0, 0))
        val round = readPng(resource("mipmap-xxxhdpi/ic_launcher_round.png"))
        assertEquals(0, round.pixel(0, 0) ushr 24)
        val note = readPng(resource("drawable-xxxhdpi/ic_stat_ticker.png"))
        var ink = 0
        var clear = 0
        for (y in 0 until note.height) {
            for (x in 0 until note.width) {
                val pixel = note.pixel(x, y)
                val alpha = pixel ushr 24 and 0xFF
                if (alpha == 0) {
                    clear++
                    continue
                }
                ink++
                assertTrue((pixel and 0xFFFFFF) == 0xFFFFFF)
            }
        }
        assertTrue(ink > 100)
        assertTrue(clear > 20)
        val mark = readPng(resource("drawable-xxxhdpi/ic_lux_wire.png"))
        var markHoles = 0
        var markOrange = 0
        for (y in 0 until mark.height) {
            for (x in 0 until mark.width) {
                val pixel = mark.pixel(x, y)
                if ((pixel ushr 24) == 0) markHoles++ else {
                    markOrange++
                    assertEquals(0xFFF7931A.toInt(), pixel)
                }
            }
        }
        assertTrue(markHoles > 50)
        assertTrue(markOrange > 50)
        val adaptive = resource("mipmap-anydpi-v26/ic_launcher.xml").readText()
        assertTrue(adaptive.contains("@color/lux_wire_paper"))
        assertTrue(adaptive.contains("@mipmap/ic_launcher_foreground"))
        assertTrue(adaptive.contains("@mipmap/ic_launcher_monochrome"))
        val widget = resource("xml/ticker_widget_info.xml").readText()
        assertTrue(widget.contains("@drawable/ic_lux_wire"))
        assertTrue(widget.contains("@layout/widget_preview"))
    }

    private fun resource(relative: String): File {
        return listOf(
            File("src/main/res/$relative"),
            File("app/src/main/res/$relative"),
        ).first { it.exists() }
    }

    private class Png(val width: Int, val height: Int, private val argb: IntArray) {
        fun pixel(x: Int, y: Int): Int = argb[y * width + x]
    }

    private fun readPng(file: File): Png {
        val data = file.readBytes()
        require(data.copyOfRange(0, 8).contentEquals(byteArrayOf(0x89.toByte(), 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)))
        var offset = 8
        var width = 0
        var height = 0
        var colorType = 0
        val idat = ArrayList<Byte>()
        while (offset + 8 <= data.size) {
            val length = data.intAt(offset)
            val type = data.copyOfRange(offset + 4, offset + 8).decodeToString()
            val chunk = data.copyOfRange(offset + 8, offset + 8 + length)
            offset += 12 + length
            when (type) {
                "IHDR" -> {
                    width = chunk.intAt(0)
                    height = chunk.intAt(4)
                    colorType = chunk[9].toInt() and 0xFF
                    require(chunk[8].toInt() == 8)
                    require(chunk[12].toInt() == 0)
                }
                "IDAT" -> idat.addAll(chunk.asList())
                "IEND" -> break
            }
        }
        val bpp = if (colorType == 6) 4 else if (colorType == 2) 3 else error("color $colorType")
        val inflated = Inflater().run {
            setInput(idat.toByteArray())
            val out = java.io.ByteArrayOutputStream()
            val buf = ByteArray(8192)
            while (!finished()) {
                val n = inflate(buf)
                if (n == 0 && needsInput()) break
                out.write(buf, 0, n)
            }
            end()
            out.toByteArray()
        }
        val stride = width * bpp
        val rows = Array(height) { ByteArray(stride) }
        var pos = 0
        val prev = ByteArray(stride)
        for (y in 0 until height) {
            val filter = inflated[pos].toInt() and 0xFF
            pos++
            val row = inflated.copyOfRange(pos, pos + stride)
            pos += stride
            for (i in row.indices) {
                val left = if (i >= bpp) row[i - bpp].toInt() and 0xFF else 0
                val up = prev[i].toInt() and 0xFF
                val upLeft = if (i >= bpp) prev[i - bpp].toInt() and 0xFF else 0
                val value = row[i].toInt() and 0xFF
                row[i] = when (filter) {
                    0 -> value
                    1 -> value + left
                    2 -> value + up
                    3 -> value + (left + up) / 2
                    4 -> value + paeth(left, up, upLeft)
                    else -> error("filter $filter")
                }.toByte()
            }
            rows[y] = row
            row.copyInto(prev)
        }
        val argb = IntArray(width * height)
        for (y in 0 until height) {
            for (x in 0 until width) {
                val i = x * bpp
                val r = rows[y][i].toInt() and 0xFF
                val g = rows[y][i + 1].toInt() and 0xFF
                val b = rows[y][i + 2].toInt() and 0xFF
                val a = if (bpp == 4) rows[y][i + 3].toInt() and 0xFF else 255
                argb[y * width + x] = (a shl 24) or (r shl 16) or (g shl 8) or b
            }
        }
        return Png(width, height, argb)
    }

    private fun paeth(a: Int, b: Int, c: Int): Int {
        val p = a + b - c
        val pa = kotlin.math.abs(p - a)
        val pb = kotlin.math.abs(p - b)
        val pc = kotlin.math.abs(p - c)
        return when {
            pa <= pb && pa <= pc -> a
            pb <= pc -> b
            else -> c
        }
    }

    private fun ByteArray.intAt(index: Int): Int {
        return ((this[index].toInt() and 0xFF) shl 24) or
            ((this[index + 1].toInt() and 0xFF) shl 16) or
            ((this[index + 2].toInt() and 0xFF) shl 8) or
            (this[index + 3].toInt() and 0xFF)
    }
}
