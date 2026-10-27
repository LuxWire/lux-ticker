package com.lux.ticker.luxwire

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class LuxWirePostsTest {
    @Test fun xTimelineBecomesPostsAndTheFirstPollDoesNotAlert() {
        val body = """
            {"data":[
              {"id":"2113409576754086202","text":"short","note_tweet":{"text":"Tesla reported income"},"attachments":{"media_keys":["3_1"]}},
              {"id":"9","text":"Desk","subscriber_only":true,"entities":{"urls":[{"expanded_url":"https://x.com/LuxWire/article/9"}]}}
            ],
            "includes":{"media":[{"media_key":"3_1","url":"https://pbs.twimg.com/card.jpg"}]}}
        """.trimIndent()
        val posts = LuxWireX.parseTimeline(body)
        assertEquals("2113409576754086202", posts[0].id)
        assertEquals("Tesla reported income", posts[0].text)
        assertEquals("https://x.com/LuxWire/status/2113409576754086202", posts[0].url)
        assertEquals("https://pbs.twimg.com/card.jpg", posts[0].imageUrl)
        assertFalse(posts[0].subscriberOnly)
        assertTrue(posts[1].subscriberOnly)
        assertEquals("https://x.com/LuxWire/article/9", posts[1].articleUrl)
        assertEquals("9", LuxWireX.parseUserId("""{"data":{"id":"9","username":"LuxWire"}}"""))

        val first = LuxWireCursor.step(primed = false, seen = emptySet(), newestFirst = posts)
        assertTrue(first.notify.isEmpty())
        assertTrue(first.primed)
        val newer = listOf(LuxWirePost("10", "Next", "https://x.com/LuxWire/status/10")) + posts
        val second = LuxWireCursor.step(primed = true, seen = first.seen, newestFirst = newer)
        assertEquals(listOf("10"), second.notify.map { it.id })
        val again = LuxWireCursor.step(primed = true, seen = second.seen, newestFirst = newer)
        assertTrue(again.notify.isEmpty())
    }

    @Test fun pkceChallengeAndTokenMatchTheXShape() {
        val challenge = LuxWireOAuth.challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk")
        assertEquals("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", challenge)
        val url = LuxWireOAuth.authorizeUrl("client", "state", challenge)
        assertTrue(url.startsWith("https://x.com/i/oauth2/authorize"))
        assertTrue(url.contains("code_challenge_method=S256"))
        assertTrue(url.contains("redirect_uri=luxwireticker"))
        val token = LuxWireOAuth.parseToken(
            """{"access_token":"abc","refresh_token":"def","expires_in":100}""",
            1_000L,
        )
        assertEquals("abc", token!!.access)
        assertEquals("def", token.refresh)
        assertEquals(101_000L, token.expiresAtMillis)
    }
}
