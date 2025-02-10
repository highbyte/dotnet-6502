// --------------------
// Fragment shader in Skia SKSL format.
// Used to render C64 borders, screen and sprites (provided as shader textures for background and foreground separatly and combined).
//
// Shader source contains placeholders in the format of #PLACEHOLDER_NAME that should be dynamically replaced with actual values before use.
//
// Skia shader language (SKSL): https://skia.org/docs/user/sksl/
//
// --------------------

// Pixels for C64 background and border, and low prio sprites
uniform shader background_and_border_texture;

// Pixels for C64 foreground (text, bitmap) and high prio sprites
uniform shader foreground_texture;

uniform half4 transparentColor;


// Maps the actual color to display on screen from the text/bitmap color drawn on bitmap_texture
half4 map_screen_color(half4 backgroundAndBorderColor, half4 foregroundColor) {

    half4 useColor;

    if (foregroundColor == transparentColor || foregroundColor == half4(0,0,0,0)) {
        useColor = backgroundAndBorderColor;
    }
    else {
        useColor = foregroundColor;
    }

    return useColor;
}

// Main function of the shader, called for each pixel on the screen
half4 main(float2 fragCoord) {

    half4 backgroundAndBorderColor = background_and_border_texture.eval(fragCoord);
    half4 foregroundColor = foreground_texture.eval(fragCoord);

    half4 useColor;

    if(fragCoord.y < #VISIBLE_HEIGHT) {
        useColor = map_screen_color(backgroundAndBorderColor, foregroundColor);
    }
    else {
        // Should not happen, show bright red color to indicate error.
        useColor = half4(1, 0, 0, 1);
    }

    return useColor;
}