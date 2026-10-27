package com.lux.ticker.ui

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.text.selection.TextSelectionColors
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.TextFieldColors
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.VisualTransformation
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.lux.ticker.LuxApp
import com.lux.ticker.data.FieldInk
import com.lux.ticker.data.PaperPalette
import com.lux.ticker.data.TickerSettings

@Composable
fun luxFieldColors(forcePaper: Boolean = false): TextFieldColors {
    val context = LocalContext.current
    val settings by LuxApp.container(context).settings.settings.collectAsStateWithLifecycle(
        initialValue = TickerSettings(),
    )
    val systemDark = isSystemInDarkTheme()
    val dark = settings.appTheme.resolveDark(systemDark)
    val paper = settings.appTheme.paperSurface(systemDark)
    val ink = Color(FieldInk.argb(forcePaper = forcePaper, paperSurface = paper, dark = dark))
    val onPaper = forcePaper || paper
    val border = if (onPaper) Color(PaperPalette.SUB) else MaterialTheme.colorScheme.outline
    val focusBorder = if (onPaper) Color(PaperPalette.ACCENT) else MaterialTheme.colorScheme.primary
    return OutlinedTextFieldDefaults.colors(
        focusedTextColor = ink,
        unfocusedTextColor = ink,
        disabledTextColor = ink.copy(alpha = 0.62f),
        errorTextColor = ink,
        cursorColor = ink,
        errorCursorColor = ink,
        selectionColors = TextSelectionColors(
            handleColor = ink,
            backgroundColor = ink.copy(alpha = 0.28f),
        ),
        focusedBorderColor = focusBorder,
        unfocusedBorderColor = border,
        disabledBorderColor = border.copy(alpha = 0.4f),
        errorBorderColor = Color(PaperPalette.LOSS),
        focusedLabelColor = ink,
        unfocusedLabelColor = ink,
        disabledLabelColor = ink.copy(alpha = 0.62f),
        errorLabelColor = Color(PaperPalette.LOSS),
        focusedPlaceholderColor = ink.copy(alpha = 0.62f),
        unfocusedPlaceholderColor = ink.copy(alpha = 0.62f),
        disabledPlaceholderColor = ink.copy(alpha = 0.4f),
        errorPlaceholderColor = ink.copy(alpha = 0.62f),
        focusedSupportingTextColor = ink,
        unfocusedSupportingTextColor = ink,
        disabledSupportingTextColor = ink.copy(alpha = 0.62f),
        errorSupportingTextColor = Color(PaperPalette.LOSS),
        focusedContainerColor = Color.Transparent,
        unfocusedContainerColor = Color.Transparent,
        disabledContainerColor = Color.Transparent,
        errorContainerColor = Color.Transparent,
    )
}

@Composable
fun LuxOutlinedField(
    value: String,
    onValueChange: (String) -> Unit,
    label: @Composable () -> Unit,
    modifier: Modifier = Modifier,
    forcePaper: Boolean = false,
    singleLine: Boolean = true,
    enabled: Boolean = true,
    isError: Boolean = false,
    supportingText: @Composable (() -> Unit)? = null,
    visualTransformation: VisualTransformation = VisualTransformation.None,
    keyboardOptions: KeyboardOptions = KeyboardOptions.Default,
    keyboardActions: KeyboardActions = KeyboardActions.Default,
    trailingIcon: @Composable (() -> Unit)? = null,
) {
    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        modifier = modifier,
        enabled = enabled,
        singleLine = singleLine,
        isError = isError,
        label = label,
        supportingText = supportingText,
        visualTransformation = visualTransformation,
        keyboardOptions = keyboardOptions,
        keyboardActions = keyboardActions,
        trailingIcon = trailingIcon,
        colors = luxFieldColors(forcePaper),
    )
}
