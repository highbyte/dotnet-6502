using static Highbyte.DotNet6502.Systems.Commodore64.Video.Vic2BitmapManager.BitmapAddressChangedEventArgs;

namespace Highbyte.DotNet6502.Systems.Commodore64.Video;

/// <summary>
/// Manager for VIC-II bitmap
/// </summary>
public class Vic2BitmapManager
{
    public const int BITMAP_SIZE = (320 * 200) / 8; // 8000 bytes, 1 byte contains 8 pixels.

    public Vic2 Vic2 { get; private set; }
    private Memory _vic2Mem => Vic2.Vic2Mem;

    // Offset into the currently selected VIC2 bank (Mem.SetMemoryConfiguration(bank))
    private ushort _bitmapAddressInVIC2Bank;
    public ushort BitmapAddressInVIC2Bank => _bitmapAddressInVIC2Bank;


    private Vic2BitmapManager() { }

    public Vic2BitmapManager(Vic2 vic2)
    {
        Vic2 = vic2;
    }

    public void NotifyBitmapAddressChanged()
    {
        OnBitmapAddressChanged(new(bitmapChangeType: BitmapChangeType.BitmapBaseAddress));
    }

    public void BitmapBaseAddressUpdate(byte bitmapBaseAddressSetting)
    {
        // From VIC 2 perspective, IO address 0xd018 bits 1-3 controls where within a VIC 2 "Bank"
        // the bitmap (when in bitmap mode) is read from. It's a offset from the start of VIC 2 memory.
        // 
        // The parameter characterBaseAddressSetting contains that 3-bit value.
        // 
        // Only the highest bit is significant, the other bits are ignored.


        // By default, this value is 2 (%010), which after ignoring the lowest bits means %000, which corresponds to
        // addresses 0x0000-0x1F40 offset from Bank below. 
        // If Bank 0 or bank 2 is selected, this points to a shadow copy of the Chargen ROM
        // %000, 0: 0x0000-0x1F3F, 0-7999.
        // %100, 4: 0x2000-0x3F3F, 8192-16191.

        var oldBitmapAddressInVIC2Bank = _bitmapAddressInVIC2Bank;
        _bitmapAddressInVIC2Bank = (bitmapBaseAddressSetting & 0b100) switch
        {
            0b000 => 0,
            0b100 => 0x2000,
            _ => throw new NotImplementedException(),
        };
        if (_bitmapAddressInVIC2Bank != oldBitmapAddressInVIC2Bank)
            NotifyBitmapAddressChanged();
    }

    public void DetectChangesToBitmapData(ushort vic2Address, byte value)
    {
        //// TODO? Needed for bitmap? Temp fix for this being called during startup before VIC2 bank is set.
        //if (BitmapAddressInVIC2Bank == 0)
        //    return;

        // Detect changes to the data within the current bitmap.
        if (vic2Address >= BitmapAddressInVIC2Bank && vic2Address < BitmapAddressInVIC2Bank + BITMAP_SIZE)
        {
            // Check if value actually changed
            if (_vic2Mem[vic2Address] != value)
            {
                // TODO:
                //// Raise event for the byte offset that contains the pixel that changed
                //var pixelByte = (byte)(vic2Address - BitmapAddressInVIC2Bank);
                //OnBitmapAddressChanged(new(
                //    bitmapChangeType: BitmapChangeType.PixelByte,
                //    pixelByte: pixelByte));
            }
        }
    }

    public event EventHandler<BitmapAddressChangedEventArgs> BitmapAddressChanged;
    protected virtual void OnBitmapAddressChanged(BitmapAddressChangedEventArgs e)
    {
        var handler = BitmapAddressChanged;
        handler?.Invoke(this, e);
    }

    public class BitmapAddressChangedEventArgs : EventArgs
    {
        public BitmapChangeType ChangeType { get; private set; }

        public enum BitmapChangeType
        {
            BitmapBaseAddress
        }

        public BitmapAddressChangedEventArgs(BitmapChangeType bitmapChangeType)
        {
            ChangeType = bitmapChangeType;
        }
    }
}
