# Typography, Button & ComboBox System

This document describes the consolidated typography, button, and ComboBox styling system for the Avalonia UI application.

## Typography Styles

The following styles are defined in `App.axaml` and can be used across all views by adding the `Classes` attribute to TextBlock elements.

### Headers

- **`h1`** - Large headers (FontSize: 20, SemiBold, White) - Used for main titles, dialog headers
- **`h2`** - Medium headers (FontSize: 16, SemiBold, White) - Used for section headers
- **`h3`** - Small headers (FontSize: 14, SemiBold, White) - Used for subsection headers

### Body Text

- **`body`** - Standard body text (FontSize: 12, Normal, #E2E8F0) - Default text content
- **`body-light`** - Light body text (FontSize: 12, Normal, #A0AEC0) - Secondary text content
- **`small`** - Small text (FontSize: 11, Normal, #A0AEC0) - Minor text, descriptions
- **`small-bold`** - Small bold text (FontSize: 11, SemiBold, #CBD5E0) - Minor emphasized text
- **`tiny`** - Very small text (FontSize: 10, Normal, #E2E8F0) - Statistics, tiny labels
- **`tiny-bold`** - Very small bold text (FontSize: 10, SemiBold, #E2E8F0) - Statistics labels

### Specific Use Cases

- **`label`** - Form labels (FontSize: 12, SemiBold, #CBD5E0) - Input field labels
- **`field-label`** - Field labels (FontSize: 12, Normal, #CBD5E0) - Form field labels
- **`section-header`** - Panel section headers (FontSize: 12, SemiBold, #CBD5E0, Margin: 0,20,0,5)
- **`status`** - Status text (FontSize: 12, Normal, #F7FAFC) - System status displays
- **`help-text`** - Help text (FontSize: 12, Normal, #A0AEC0, TextWrapping: Wrap) - Descriptions and help
- **`overlay`** - Overlay text (FontSize: 16, Italic, #718096) - Placeholder text over displays

### State-Specific Text

- **`error`** - Error messages (FontSize: 14, SemiBold, #F44336)
- **`success`** - Success messages (FontSize: 14, SemiBold, #68D391)
- **`warning`** - Warning messages (FontSize: 14, SemiBold, #F6AD55)

### Special Text

- **`monospace`** - Monospace text (Consolas/Monaco/Courier New, FontSize: 12, LightGray) - Code and technical text

## Button Styles

The following button styles provide consistent appearance and semantic meaning across the application.

### Semantic Button Classes

- **`primary`** - Primary actions (Start, Save, Download ROMs) - Green background, bold text
- **`secondary`** - Secondary actions (Reset, Load from files) - Blue background, medium weight
- **`warning`** - Warning actions (Pause) - Orange background, medium weight  
- **`danger`** - Destructive actions (Stop, Exit, Clear) - Red background, medium weight
- **`accent`** - Special features (C64 Config) - Purple background, medium weight
- **`cancel`** - Cancel/dismiss actions - Gray background, medium weight
- **`text`** - Text-only buttons (Show Details) - No background, blue text

### Size Variants

- **`small`** - Compact buttons (FontSize: 11, Padding: 8,4, MinHeight: 28)
- **`large`** - Important actions (FontSize: 14, Padding: 20,10, MinHeight: 44)
- **`compact`** - Minimal buttons (FontSize: 11, Padding: 6,3, MinHeight: 24)

### Default Button Style

All buttons inherit these base properties:
- FontSize: 12
- FontWeight: Medium  
- Padding: 12,6
- CornerRadius: 4
- BorderThickness: 1
- Cursor: Hand

## ComboBox Styles

The following ComboBox styles provide consistent font sizing and appearance across the application.

### Size Variants

- **Default** - Standard ComboBoxes (FontSize: 12, Padding: 8,6, MinHeight: 32)
- **`small`** - Compact ComboBoxes (FontSize: 11, Padding: 6,4, MinHeight: 28) - Used for port selections
- **`large`** - Important selections (FontSize: 14, Padding: 10,8, MinHeight: 38) - Used for prominent choices
- **`compact`** - Minimal ComboBoxes (FontSize: 11, Padding: 6,3, MinHeight: 26) - Used in tight spaces like side panels

### Default ComboBox Style

All ComboBoxes inherit these base properties:
- FontSize: 12
- FontWeight: Normal
- Padding: 8,6
- MinHeight: 32
- CornerRadius: 4

### ComboBox Item Text

ComboBox items automatically inherit the font size from their parent ComboBox style, ensuring consistent text sizing in dropdown lists.

## Usage Examples

### Typography

Instead of specifying font properties inline:

```xml
<!-- Old way -->
<TextBlock Text="System Settings" 
           FontSize="16" 
           FontWeight="SemiBold" 
           Foreground="White"/>
```

Use the typography classes:

```xml
<!-- New way -->
<TextBlock Text="System Settings" 
           Classes="h2"/>
```

### Buttons

Instead of specifying button properties inline:

```xml
<!-- Old way -->
<Button Content="Start" 
        Background="#38A169" 
        Foreground="White"
        FontWeight="SemiBold"/>
```

Use the semantic button classes:

```xml
<!-- New way -->
<Button Content="Start" 
        Classes="primary"/>
```

### ComboBoxes

Instead of leaving ComboBoxes unstyled or setting font sizes inline:

```xml
<!-- Old way -->
<ComboBox ItemsSource="{Binding Options}"
          FontSize="11"/>
```

Use the semantic ComboBox classes:

```xml
<!-- New way -->
<ComboBox ItemsSource="{Binding Options}"
          Classes="small"/>
```

### Multiple Classes

You can combine classes for more specific styling:

```xml
<!-- Small primary button -->
<Button Content="Quick Save" 
        Classes="primary small"/>

<!-- Large danger button -->
<Button Content="Emergency Stop" 
        Classes="danger large"/>

<!-- Compact ComboBox for narrow spaces -->
<ComboBox ItemsSource="{Binding Systems}"
          Classes="compact"/>
```

## ComboBox Usage Guidelines

- **Default** - Use for main configuration options and standard form fields
- **`small`** - Use for port selections, secondary options, and compact forms
- **`large`** - Use for important system selections and prominent choices
- **`compact`** - Use in side panels, toolbars, and very constrained spaces

### Examples by Context

```xml
<!-- Main system selection (prominent choice) -->
<ComboBox ItemsSource="{Binding Systems}" Classes="large"/>

<!-- Render provider selection (standard config) -->
<ComboBox ItemsSource="{Binding RenderProviders}"/>

<!-- Joystick port selection (compact choice) -->
<ComboBox ItemsSource="{Binding JoystickPorts}" Classes="small"/>

<!-- System selection in side panel (tight space) -->
<ComboBox ItemsSource="{Binding Systems}" Classes="compact"/>
```

## Benefits

1. **Consistency** - All text, buttons, and ComboBoxes across the application use the same predefined styles
2. **Maintainability** - Font sizes, colors, and control styles can be changed in one place
3. **Scalability** - Easy to add new typography, button, or ComboBox variants
4. **Semantic clarity** - Code is cleaner and more meaningful with descriptive class names
5. **Accessibility** - Consistent sizing and contrast ratios
6. **Theming** - Easy to implement dark/light themes by modifying central styles
7. **Responsive design** - Different sizes for different contexts and space constraints

## Button Color Palette

The button system uses a consistent color palette:

- **Primary** (#38A169) - Success green for main actions
- **Secondary** (#3182CE) - Blue for secondary actions  
- **Warning** (#DD6B20) - Orange for potentially disruptive actions
- **Danger** (#E53E3E) - Red for destructive actions
- **Accent** (#805AD5) - Purple for special features
- **Cancel** (#4A5568) - Gray for cancel/dismiss actions
- **Text** (#3182CE) - Blue text for text-only buttons

## Typography Color Palette

The typography system uses a consistent color palette:

- **White** (#FFFFFF) - Primary headers and important text
- **#F7FAFC** - Status text, high contrast
- **#E2E8F0** - Standard body text, good contrast
- **#CBD5E0** - Labels and secondary headers
- **#A0AEC0** - Helper text and descriptions
- **#718096** - Placeholder/overlay text
- **LightGray** - Monospace text
- **#F44336** - Error red
- **#68D391** - Success green
- **#F6AD55** - Warning orange

## Adding New Styles

### Typography

To add new typography styles, add them to the `Application.Styles` section in `App.axaml`:

```xml
<Style Selector="TextBlock.my-new-style">
    <Setter Property="FontSize" Value="15"/>
    <Setter Property="FontWeight" Value="Medium"/>
    <Setter Property="Foreground" Value="#CustomColor"/>
</Style>
```

### Buttons

To add new button styles, add them to the `Application.Styles` section in `App.axaml`:

```xml
<Style Selector="Button.my-new-button">
    <Setter Property="FontSize" Value="12"/>
    <Setter Property="FontWeight" Value="Medium"/>
    <Setter Property="Background" Value="#CustomColor"/>
    <Setter Property="Foreground" Value="White"/>
    <Setter Property="BorderBrush" Value="#DarkerCustomColor"/>
</Style>
```

### ComboBoxes

To add new ComboBox styles, add them to the `Application.Styles` section in `App.axaml`:

```xml
<Style Selector="ComboBox.my-new-combo">
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="Padding" Value="9,7"/>
    <Setter Property="MinHeight" Value="35"/>
</Style>

<!-- Don't forget the item text styling -->
<Style Selector="ComboBox.my-new-combo TextBlock">
    <Setter Property="FontSize" Value="13"/>
</Style>
```

Then use them in your views:

```xml
<TextBlock Text="My Text" Classes="my-new-style"/>
<Button Content="My Action" Classes="my-new-button"/>
<ComboBox ItemsSource="{Binding Items}" Classes="my-new-combo"/>