// --------------------
// Fragment shader in Skia SKSL format.
// Used to render C64 screen and sprites (provided as shader textures), together with color information per raster line (in a separate shader texture used for this data).
//
// Skia shader language (SKSL): https://skia.org/docs/user/sksl/
// --------------------

// Pixels for C64 text/bitmap
uniform shader bitmap_texture;

// Pixels for C64 sprites
uniform shader sprites_texture;

// The actual colors (per raster line) to display as border, background, sprite colors. Replaces the pixels colors from bitmap_texture and sprites_texture.
uniform shader line_data_map;

// The color used to draw border and background colors in bitmap_texture
uniform half4 bg0Color;
uniform half4 bg1Color;
uniform half4 bg2Color;
uniform half4 bg3Color;

uniform half4 borderColor;

// The color used to draw border and background colors in sprites_texture
uniform half4 spriteLowPrioMultiColor0;
uniform half4 spriteLowPrioMultiColor1;

uniform half4 spriteHighPrioMultiColor0;
uniform half4 spriteHighPrioMultiColor1;

uniform half4 sprite0LowPrioColor;
uniform half4 sprite1LowPrioColor;
uniform half4 sprite2LowPrioColor;
uniform half4 sprite3LowPrioColor;
uniform half4 sprite4LowPrioColor;
uniform half4 sprite5LowPrioColor;
uniform half4 sprite6LowPrioColor;
uniform half4 sprite7LowPrioColor;

uniform half4 sprite0HighPrioColor;
uniform half4 sprite1HighPrioColor;
uniform half4 sprite2HighPrioColor;
uniform half4 sprite3HighPrioColor;
uniform half4 sprite4HighPrioColor;
uniform half4 sprite5HighPrioColor;
uniform half4 sprite6HighPrioColor;
uniform half4 sprite7HighPrioColor;

// Get one specific data/color (lineIndex) from the line_data_map at raster line (line)
half4 get_line_data(float lineIndex, float line) {
    // Assume image in line_data_map is x pixel wide (bg0, bg1, bg2, bg3, border colors, etc.), and y (number of main screen lines) pixels high.

    // For images, Skia GSL use the common convention that the centers are at half-pixel offsets. 
    // So to sample the top-left pixel in an image shader, you'd want to pass (0.5, 0.5) as coords.
    // The next (to the right) pixel would be (1.5, 0.5).
    // The next (to the below ) pixel would be (0.5, 1.5).

    return line_data_map.eval(float2(0.5 + lineIndex, 0.5 + line));
}

// Map sprite color in spriteColor (that was drawn on sprites_texture) to the actual color to display (from line_data_map on the specified line)
half4 map_sprite_color(half4 spriteColor, float line) {

    half4 useColor;

    if(spriteColor == spriteLowPrioMultiColor0 || spriteColor == spriteHighPrioMultiColor0) {
        useColor = get_line_data(#SPRITE_MULTICOLOR0_INDEX, line);
    }
    else if(spriteColor == spriteLowPrioMultiColor1 || spriteColor == spriteHighPrioMultiColor1) {
        useColor = get_line_data(#SPRITE_MULTICOLOR1_INDEX, line);
    }

    else if(spriteColor == sprite0LowPrioColor || spriteColor == sprite0HighPrioColor) {
        useColor = get_line_data(#SPRITE0_COLOR_INDEX, line);
    }
    else if(spriteColor == sprite1LowPrioColor || spriteColor == sprite1HighPrioColor) {
        useColor = get_line_data(#SPRITE1_COLOR_INDEX, line);
    }
    else if(spriteColor == sprite2LowPrioColor || spriteColor == sprite2HighPrioColor) {
        useColor = get_line_data(#SPRITE2_COLOR_INDEX, line);
    }
    else if(spriteColor == sprite3LowPrioColor || spriteColor == sprite3HighPrioColor) {
        useColor = get_line_data(#SPRITE3_COLOR_INDEX, line);
    }
    else if(spriteColor == sprite4LowPrioColor || spriteColor == sprite4HighPrioColor) {
        useColor = get_line_data(#SPRITE4_COLOR_INDEX, line);
    }
    else if(spriteColor == sprite5LowPrioColor || spriteColor == sprite5HighPrioColor) {
        useColor = get_line_data(#SPRITE5_COLOR_INDEX, line);
    }
    else if(spriteColor == sprite6LowPrioColor || spriteColor == sprite6HighPrioColor) {
        useColor = get_line_data(#SPRITE6_COLOR_INDEX, line);
    }
    else if(spriteColor == sprite7LowPrioColor || spriteColor == sprite7HighPrioColor) {
        useColor = get_line_data(#SPRITE7_COLOR_INDEX, line);
    }

    else {
        // Should not happen, show bright purple color to indicate error.
        useColor = half4(1, 0, 1, 1);
    }

    return useColor;    
}

// Maps the actual color to display on screen from the text/bitmap color drawn on bitmap_texture, and the sprite color drawn on sprites_texture.
half4 map_screen_color(half4 textAndBitmapColor, half4 spriteColor, float line) {

    half4 useColor;
    float2 lineCoord;

    if(spriteColor.b == #HIGH_PRIO_SPRITE_BLUE_SHADER) {
        // Sprite pixel with specific Blue color indicates high-prio sprite here, and should be shown instead of background or border color.
        useColor = map_sprite_color(spriteColor, line);
    }
    else if(textAndBitmapColor == borderColor) {
        // Normal border color indicates border color could used.

        // But if a sprite pixel from a low prio sprite is drawn at this position, use the sprite color instead of border color.
        // Sprite pixel with specific Blue color indicates low-prio sprite here, and should be shown instead of border color.
        if(spriteColor.b == #LOW_PRIO_SPRITE_BLUE_SHADER) {
            // Use sprite color
            useColor = map_sprite_color(spriteColor, line);
        }
        else {
            // Use border color
            useColor = get_line_data(#BORDER_COLOR_INDEX, line);
        }
    }
    else if((textAndBitmapColor + bg0Color) == bg0Color) {
        // Normal text/bitmap screen indicates background color could used.

        // But if a sprite pixel from a low prio sprite is drawn at this position, use the sprite color instead of background color.
        // Sprite pixel with specific Blue color indicates low-prio sprite here, and should be shown instead of background color.
        if(spriteColor.b == #LOW_PRIO_SPRITE_BLUE_SHADER) {
            // Use sprite color
            useColor = map_sprite_color(spriteColor, line);
        }
        else {
            // Use background color
            useColor = get_line_data(#BG0_COLOR_INDEX, line);
        }
    }
    else if(textAndBitmapColor == bg1Color) {
        useColor = get_line_data(#BG1_COLOR_INDEX, line);
    }
    else if(textAndBitmapColor == bg2Color) {
        useColor = get_line_data(#BG2_COLOR_INDEX, line);
    }
    else if(textAndBitmapColor == bg3Color) {
        useColor = get_line_data(#BG3_COLOR_INDEX, line);
    }
    else {
        useColor = textAndBitmapColor;    // Not a color that should be transformed (i.e. foreground color of text or bitmap)
    }
    return useColor;
}

// Main function of the shader, called for each pixel on the screen
half4 main(float2 fragCoord) {

    half4 textAndBitmapColor = bitmap_texture.eval(fragCoord);
    half4 spriteColor = sprites_texture.eval(fragCoord);

    half4 useColor;

    if(fragCoord.y < #VISIBLE_HEIGHT) {
        useColor = map_screen_color(textAndBitmapColor, spriteColor, fragCoord.y);
    }
    else {
        // Should not happen, show bright red color to indicate error.
        useColor = half4(1, 0, 0, 1);
    }

    return useColor;
}