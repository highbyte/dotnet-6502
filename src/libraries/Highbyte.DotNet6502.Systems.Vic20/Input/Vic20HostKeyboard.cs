using Highbyte.DotNet6502.Systems.Input;
using Highbyte.DotNet6502.Systems.Vic20.TimerAndPeripheral;

namespace Highbyte.DotNet6502.Systems.Vic20.Input;

/// <summary>
/// Maps host-neutral <see cref="HostKey"/> values to VIC-20 keyboard-matrix keys.
///
/// Modelled directly on C64HostKeyboard so the structure is familiar.
/// Multi-key host combinations (Shift+key) that produce a single VIC-20 key
/// are listed with both host keys in the array — the most-specific match wins.
/// </summary>
public class Vic20HostKeyboard
{
    /// <summary>
    /// Maps host key combination → one or more VIC-20 matrix keys to press simultaneously.
    /// Dictionary key is an array of host keys that must ALL be held for the mapping to fire.
    /// </summary>
    public Dictionary<HostKey[], Vic20Key[]> HostKeyToVic20KeyMap = new()
    {
        // Letters
        { new[] { HostKey.KeyA }, new[] { Vic20Key.A } },
        { new[] { HostKey.KeyB }, new[] { Vic20Key.B } },
        { new[] { HostKey.KeyC }, new[] { Vic20Key.C } },
        { new[] { HostKey.KeyD }, new[] { Vic20Key.D } },
        { new[] { HostKey.KeyE }, new[] { Vic20Key.E } },
        { new[] { HostKey.KeyF }, new[] { Vic20Key.F } },
        { new[] { HostKey.KeyG }, new[] { Vic20Key.G } },
        { new[] { HostKey.KeyH }, new[] { Vic20Key.H } },
        { new[] { HostKey.KeyI }, new[] { Vic20Key.I } },
        { new[] { HostKey.KeyJ }, new[] { Vic20Key.J } },
        { new[] { HostKey.KeyK }, new[] { Vic20Key.K } },
        { new[] { HostKey.KeyL }, new[] { Vic20Key.L } },
        { new[] { HostKey.KeyM }, new[] { Vic20Key.M } },
        { new[] { HostKey.KeyN }, new[] { Vic20Key.N } },
        { new[] { HostKey.KeyO }, new[] { Vic20Key.O } },
        { new[] { HostKey.KeyP }, new[] { Vic20Key.P } },
        { new[] { HostKey.KeyQ }, new[] { Vic20Key.Q } },
        { new[] { HostKey.KeyR }, new[] { Vic20Key.R } },
        { new[] { HostKey.KeyS }, new[] { Vic20Key.S } },
        { new[] { HostKey.KeyT }, new[] { Vic20Key.T } },
        { new[] { HostKey.KeyU }, new[] { Vic20Key.U } },
        { new[] { HostKey.KeyV }, new[] { Vic20Key.V } },
        { new[] { HostKey.KeyW }, new[] { Vic20Key.W } },
        { new[] { HostKey.KeyX }, new[] { Vic20Key.X } },
        { new[] { HostKey.KeyY }, new[] { Vic20Key.Y } },
        { new[] { HostKey.KeyZ }, new[] { Vic20Key.Z } },

        // Digits
        { new[] { HostKey.Digit0 }, new[] { Vic20Key.Zero } },
        { new[] { HostKey.Digit1 }, new[] { Vic20Key.One } },
        { new[] { HostKey.Digit2 }, new[] { Vic20Key.Two } },
        { new[] { HostKey.Digit3 }, new[] { Vic20Key.Three } },
        { new[] { HostKey.Digit4 }, new[] { Vic20Key.Four } },
        { new[] { HostKey.Digit5 }, new[] { Vic20Key.Five } },
        { new[] { HostKey.Digit6 }, new[] { Vic20Key.Six } },
        { new[] { HostKey.Digit7 }, new[] { Vic20Key.Seven } },
        { new[] { HostKey.Digit8 }, new[] { Vic20Key.Eight } },
        { new[] { HostKey.Digit9 }, new[] { Vic20Key.Nine } },

        // Common symbols (unshifted)
        { new[] { HostKey.Minus  }, new[] { Vic20Key.Minus   } },
        { new[] { HostKey.Equal  }, new[] { Vic20Key.Equal   } },
        { new[] { HostKey.Period }, new[] { Vic20Key.Period  } },
        { new[] { HostKey.Comma  }, new[] { Vic20Key.Comma   } },
        { new[] { HostKey.Slash  }, new[] { Vic20Key.Slash   } },
        { new[] { HostKey.Semicolon }, new[] { Vic20Key.Semicolon } },

        // Shift+symbol combinations → VIC-20 symbols
        { new[] { HostKey.ShiftLeft,  HostKey.Equal   }, new[] { Vic20Key.Plus     } }, // +
        { new[] { HostKey.ShiftRight, HostKey.Equal   }, new[] { Vic20Key.Plus     } },
        { new[] { HostKey.ShiftLeft,  HostKey.Semicolon }, new[] { Vic20Key.Colon  } }, // :
        { new[] { HostKey.ShiftRight, HostKey.Semicolon }, new[] { Vic20Key.Colon  } },
        { new[] { HostKey.ShiftLeft,  HostKey.Digit8  }, new[] { Vic20Key.Asterisk } }, // *
        { new[] { HostKey.ShiftRight, HostKey.Digit8  }, new[] { Vic20Key.Asterisk } },

        // Navigation / editing
        { new[] { HostKey.Enter     }, new[] { Vic20Key.Return    } },
        { new[] { HostKey.Space     }, new[] { Vic20Key.Space     } },
        { new[] { HostKey.Backspace }, new[] { Vic20Key.Delete    } },
        { new[] { HostKey.Delete    }, new[] { Vic20Key.Delete    } },
        { new[] { HostKey.Home      }, new[] { Vic20Key.Home      } },

        // Cursor keys — VIC-20 has one cursor-right key (shift = left) and one cursor-down (shift = up).
        { new[] { HostKey.ArrowRight }, new[] { Vic20Key.CrsrRight } },
        { new[] { HostKey.ArrowLeft  }, new[] { Vic20Key.LShift, Vic20Key.CrsrRight } },
        { new[] { HostKey.ArrowDown  }, new[] { Vic20Key.CrsrDown  } },
        { new[] { HostKey.ArrowUp    }, new[] { Vic20Key.LShift, Vic20Key.CrsrDown  } },

        // Modifier keys
        { new[] { HostKey.ShiftLeft  }, new[] { Vic20Key.LShift  } },
        { new[] { HostKey.ShiftRight }, new[] { Vic20Key.RShift  } },
        { new[] { HostKey.ControlLeft  }, new[] { Vic20Key.CBM   } }, // Ctrl → Commodore key
        { new[] { HostKey.ControlRight }, new[] { Vic20Key.CBM   } },
        { new[] { HostKey.Escape       }, new[] { Vic20Key.RunStop } }, // Esc → RUN/STOP

        // Function keys
        { new[] { HostKey.F1 }, new[] { Vic20Key.F1 } },
        { new[] { HostKey.F3 }, new[] { Vic20Key.F3 } },
        { new[] { HostKey.F5 }, new[] { Vic20Key.F5 } },
        { new[] { HostKey.F7 }, new[] { Vic20Key.F7 } },
        // F2/F4/F6/F8 are shift+F1/F3/F5/F7 on the VIC-20
        { new[] { HostKey.F2 }, new[] { Vic20Key.LShift, Vic20Key.F1 } },
        { new[] { HostKey.F4 }, new[] { Vic20Key.LShift, Vic20Key.F3 } },
        { new[] { HostKey.F6 }, new[] { Vic20Key.LShift, Vic20Key.F5 } },
        { new[] { HostKey.F8 }, new[] { Vic20Key.LShift, Vic20Key.F7 } },
    };
}
