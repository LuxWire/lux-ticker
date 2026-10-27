import java.util.Properties

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
}

val keystorePropertiesFile = rootProject.file("keystore/keystore.properties")
val keystoreProperties = Properties()
if (keystorePropertiesFile.exists()) {
    keystorePropertiesFile.inputStream().use { keystoreProperties.load(it) }
}

android {
    namespace = "com.lux.ticker"
    compileSdk {
        version = release(37) {
            minorApiLevel = 2
        }
    }
    buildToolsVersion = "37.0.0"

    defaultConfig {
        applicationId = "com.lux.ticker"
        minSdk = 29
        targetSdk = 37
        versionCode = 31
        versionName = "1.4.8"
        // Google and X are enabled on the Supabase project. The buttons use that OAuth.
        buildConfigField("boolean", "ACCOUNT_OAUTH", "true")
        val local = Properties()
        val localFile = rootProject.file("local.properties")
        if (localFile.exists()) localFile.inputStream().use { local.load(it) }
        fun configString(key: String): String {
            val raw = local.getProperty(key).orEmpty().replace("\n", "")
            val escaped = raw.replace("\\", "\\\\").replace("\"", "\\\"")
            return "\"$escaped\""
        }
        buildConfigField("String", "X_BEARER", configString("x.bearer"))
        buildConfigField("String", "X_CLIENT_ID", configString("x.clientId"))
        val supabase = Properties()
        val supabaseFile = rootProject.file("supabase.properties")
        if (supabaseFile.exists()) supabaseFile.inputStream().use { supabase.load(it) }
        fun supabaseString(key: String): String {
            val raw = supabase.getProperty(key).orEmpty().replace("\n", "").trim()
            val escaped = raw.replace("\\", "\\\\").replace("\"", "\\\"")
            return "\"$escaped\""
        }
        buildConfigField("String", "SUPABASE_URL", supabaseString("supabase.url"))
        buildConfigField("String", "SUPABASE_ANON_KEY", supabaseString("supabase.anonKey"))
    }

    signingConfigs {
        if (keystorePropertiesFile.exists()) {
            create("release") {
                storeFile = rootProject.file(keystoreProperties.getProperty("storeFile"))
                storePassword = keystoreProperties.getProperty("storePassword")
                keyAlias = keystoreProperties.getProperty("keyAlias")
                keyPassword = keystoreProperties.getProperty("keyPassword")
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            signingConfig = if (keystorePropertiesFile.exists()) {
                signingConfigs.getByName("release")
            } else {
                signingConfigs.getByName("debug")
            }
        }
    }

    // Play upload builds are signed with a separate upload key. The path is passed in
    // LUX_UPLOAD_PROPS and is never stored in the repo. Sideload release keeps the original key.
    val uploadPropsPath = providers.environmentVariable("LUX_UPLOAD_PROPS").orNull
        ?: providers.gradleProperty("lux.upload.props").orNull
    val uploadPropsFile = uploadPropsPath?.takeIf { it.isNotBlank() }?.let { file(it) }?.takeIf { it.isFile }
    if (uploadPropsFile != null) {
        val uploadProps = Properties().apply {
            uploadPropsFile.inputStream().use { load(it) }
        }
        signingConfigs.create("upload") {
            storeFile = file(uploadProps.getProperty("storeFile"))
            storePassword = uploadProps.getProperty("storePassword")
            keyAlias = uploadProps.getProperty("keyAlias")
            keyPassword = uploadProps.getProperty("keyPassword")
        }
        val releaseBuildType = buildTypes.getByName("release")
        buildTypes.create("upload") {
            initWith(releaseBuildType)
            matchingFallbacks.add("release")
            isMinifyEnabled = false
            signingConfig = signingConfigs.getByName("upload")
            // A Play artifact must not embed an X bearer or client id from the build machine.
            buildConfigField("String", "X_BEARER", "\"\"")
            buildConfigField("String", "X_CLIENT_ID", "\"\"")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    lint {
        abortOnError = false
        checkReleaseBuilds = false
    }

    testOptions {
        unitTests.isIncludeAndroidResources = true
        unitTests.isReturnDefaultValues = true
    }

    packaging {
        resources {
            excludes += "/META-INF/{AL2.0,LGPL2.1}"
        }
    }
}

kotlin {
    compilerOptions {
        jvmTarget.set(org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17)
    }
}

dependencies {
    val composeBom = platform("androidx.compose:compose-bom:2026.09.00")
    implementation(composeBom)
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.foundation:foundation")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.activity:activity-compose:1.13.0")
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.11.0")
    implementation("androidx.lifecycle:lifecycle-runtime-compose:2.11.0")
    implementation("androidx.lifecycle:lifecycle-viewmodel-compose:2.11.0")
    implementation("androidx.navigation:navigation-compose:2.10.2")
    implementation("androidx.datastore:datastore-preferences:1.3.0")
    implementation("androidx.security:security-crypto:1.0.0")
    implementation("androidx.core:core-ktx:1.19.1")
    implementation("androidx.browser:browser:1.8.0")
    implementation("com.squareup.okhttp3:okhttp:4.12.0")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.11.0")
    implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.12.0")

    testImplementation("junit:junit:4.13.2")
    testImplementation("org.robolectric:robolectric:4.17")
    testImplementation("androidx.test:core:1.7.0")
    debugImplementation("androidx.compose.ui:ui-tooling")
    debugImplementation("androidx.compose.ui:ui-tooling-preview")
}

tasks.withType<org.gradle.api.tasks.testing.Test>().configureEach {
    systemProperty(
        "lux.screenshot.dir",
        layout.buildDirectory.dir("reports/screenshots").get().asFile.absolutePath,
    )
    maxHeapSize = "1536m"
}
