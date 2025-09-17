// --------------------
// Fragment shader in Skia SKSL format.
// Used to render two images combined. One image for background, and another for foreground.
//
// Shader source contains placeholders in the format of #PLACEHOLDER_NAME that should be dynamically replaced with actual values before use.
//
// Skia shader language (SKSL): https://skia.org/docs/user/sksl/
//
// --------------------

// Pixels background
uniform shader background_texture;

// Pixels foreground
uniform shader foreground_texture;

// Main function of the shader, called for each pixel on the screen
half4 main(float2 fragCoord) {

    half4 backgroundColor = background_texture.eval(fragCoord);
    half4 foregroundColor = foreground_texture.eval(fragCoord);

    // Blend the top texture over the bottom texture based on the foregroundColor texture's alpha
    return mix(backgroundColor, foregroundColor, foregroundColor.a);

    // Debug: set color to red if outside of visible area
//    half4 useColor;
//    if(fragCoord.y < #VISIBLE_HEIGHT) {
//        useColor = mix(backgroundColor, foregroundColor, foregroundColor.a);
//    }
//    else {
//        // Should not happen, show bright red color to indicate error.
//        useColor = half4(1, 0, 0, 1);
//    }
//    return useColor;
}