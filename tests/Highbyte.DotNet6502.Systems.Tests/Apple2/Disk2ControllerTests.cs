using Highbyte.DotNet6502.Systems.Apple2.Disk2;
using Highbyte.DotNet6502.Systems.Apple2.DiskImage;
using Apple2System = Highbyte.DotNet6502.Systems.Apple2.Apple2;

namespace Highbyte.DotNet6502.Systems.Tests.Apple2;

public class Disk2ControllerTests
{
    /// <summary>
    /// The CPU clock the drive is timed against. The disk turns at a fixed rate, so a test that
    /// wants the next byte has to let time pass for it — see
    /// <see cref="A_Byte_Takes_A_Byte_Time_To_Pass_Under_The_Head"/>.
    /// </summary>
    private ulong _cycles;

    private static byte[] BuildDiskImage()
    {
        var image = new byte[DskParser.DiskImageSize];
        for (var i = 0; i < image.Length; i++)
            image[i] = (byte)(i * 31);
        return image;
    }

    private static byte[] BuildBootRom()
    {
        var rom = new byte[Disk2Controller.BootRomSize];
        // Real signature bytes the Autostart slot scan checks, then a recognizable fill.
        rom[0] = 0xA2; rom[1] = 0x20; rom[2] = 0xA0; rom[3] = 0x00;
        rom[4] = 0xA2; rom[5] = 0x03; rom[6] = 0x86; rom[7] = 0x3C;
        for (var i = 8; i < rom.Length; i++)
            rom[i] = (byte)i;
        return rom;
    }

    private Disk2Controller CreateEnabledController()
    {
        var controller = new Disk2Controller(() => _cycles);
        controller.SetBootRom(BuildBootRom());
        controller.InsertDiskImage(BuildDiskImage());
        controller.BusAccess(0xC0E9);   // motor on
        controller.BusAccess(0xC0EE);   // Q7 off: read mode
        _cycles += Disk2Controller.CyclesPerNibble;   // let the first byte come round
        controller.BusAccess(0xC0EC);   // Q6 off — consumes the byte now under the head
        return controller;
    }

    /// <summary>Reads the next delivered nibble, skipping non-data values.</summary>
    private byte ReadNibble(Disk2Controller controller)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var value = controller.BusAccess(0xC0EC);
            if ((value & 0x80) != 0)
                return value;
            // Not ready: wait a byte time for the next one, which is what a poll loop does.
            _cycles += Disk2Controller.CyclesPerNibble;
        }
        Assert.Fail("No nibble delivered within 4 reads.");
        return 0;
    }

    /// <summary>
    /// A drive whose motor has never been switched on is not turning. The one-shot that keeps the
    /// disk coasting after a motor-off is timed against the CPU's cumulative cycle count, which is
    /// also 0 at power-on — so treating "never switched off" as "switched off at cycle 0" would put
    /// a brand new drive inside its own spin-down window for its first million cycles.
    /// </summary>
    [Fact]
    public void Drive_Is_Not_Spinning_Before_The_Motor_Has_Ever_Been_Switched_On()
    {
        ulong cycles = 0;
        var controller = new Disk2Controller(() => cycles);
        controller.SetBootRom(BuildBootRom());
        controller.InsertDiskImage(BuildDiskImage());

        Assert.False(controller.IsMotorOn);
        Assert.False(controller.IsSpinning);

        // Still not spinning a little way into the machine's life (inside the spin-down window).
        cycles = Disk2Controller.SpinDownCycles / 2;
        Assert.False(controller.IsSpinning);
    }

    [Fact]
    public void Motor_Keeps_Spinning_Through_The_Spin_Down_Window_After_Being_Switched_Off()
    {
        ulong cycles = 1_000;
        var controller = new Disk2Controller(() => cycles);
        controller.SetBootRom(BuildBootRom());
        controller.InsertDiskImage(BuildDiskImage());

        controller.BusAccess(0xC0E9);   // motor on
        Assert.True(controller.IsSpinning);

        controller.BusAccess(0xC0E8);   // motor off: the one-shot starts here
        Assert.False(controller.IsMotorOn);
        Assert.True(controller.IsSpinning);

        cycles += Disk2Controller.SpinDownCycles - 1;
        Assert.True(controller.IsSpinning);

        cycles += 1;
        Assert.False(controller.IsSpinning);
    }

    [Fact]
    public void Controller_Is_Enabled_Only_With_Both_Boot_Rom_And_Disk()
    {
        var controller = new Disk2Controller(() => _cycles);
        Assert.False(controller.IsEnabled);

        controller.SetBootRom(BuildBootRom());
        Assert.False(controller.IsEnabled);

        controller.InsertDiskImage(BuildDiskImage());
        Assert.True(controller.IsEnabled);

        controller.RemoveDiskImage();
        Assert.False(controller.IsEnabled);
    }

    [Fact]
    public void SetBootRom_Rejects_Wrong_Size()
    {
        Assert.Throws<DotNet6502Exception>(() => new Disk2Controller(() => _cycles).SetBootRom(new byte[512]));
    }

    [Fact]
    public void ReadBootRom_Returns_Rom_Bytes_When_Enabled_And_Unconnected_When_Not()
    {
        var controller = new Disk2Controller(() => _cycles);
        controller.SetBootRom(BuildBootRom());

        // No disk: the slot looks empty so the Autostart scan falls through to BASIC.
        Assert.Equal(0xFF, controller.ReadBootRom(0xC600));

        controller.InsertDiskImage(BuildDiskImage());
        Assert.Equal(0xA2, controller.ReadBootRom(0xC600));
        Assert.Equal(0x20, controller.ReadBootRom(0xC601));
        Assert.Equal((byte)0x42, controller.ReadBootRom(0xC642));
    }

    [Fact]
    public void Motor_And_Drive_Select_Soft_Switches_Update_State()
    {
        var controller = new Disk2Controller(() => _cycles);
        Assert.False(controller.IsMotorOn);

        controller.BusAccess(0xC0E9);
        Assert.True(controller.IsMotorOn);
        controller.BusAccess(0xC0E8);
        Assert.False(controller.IsMotorOn);

        Assert.Equal(1, controller.SelectedDrive);
        controller.BusAccess(0xC0EB);
        Assert.Equal(2, controller.SelectedDrive);
        controller.BusAccess(0xC0EA);
        Assert.Equal(1, controller.SelectedDrive);
    }

    [Fact]
    public void Data_Read_Requires_Motor_On_And_Drive_1()
    {
        var controller = CreateEnabledController();

        controller.BusAccess(0xC0E8);                 // motor off
        _cycles += Disk2Controller.SpinDownCycles;    // and the one-shot expires: the disk stops
        Assert.Equal(0xFF, controller.BusAccess(0xC0EC));

        controller.BusAccess(0xC0E9);   // motor on
        controller.BusAccess(0xC0EB);   // the absent drive 2
        Assert.Equal(0xFF, controller.BusAccess(0xC0EC));
    }

    [Fact]
    public void Successive_Reads_A_Byte_Time_Apart_Deliver_The_Track_In_Order_And_Wrap()
    {
        var image = BuildDiskImage();
        var track = Disk2TrackNibblizer.BuildNibbleTracks(image)[0];

        var controller = new Disk2Controller(() => _cycles);
        controller.SetBootRom(BuildBootRom());
        controller.InsertDiskImage(image);   // the head sits at position 0 when the disk goes in
        controller.BusAccess(0xC0E9);        // motor on
        controller.BusAccess(0xC0EE);        // Q7 off: read mode

        // The byte at index i passes under the head i byte times later, and the last one is
        // followed by index 0 again — the next revolution.
        for (var i = 1; i <= track.Length; i++)
        {
            _cycles += Disk2Controller.CyclesPerNibble;
            Assert.Equal(track[i % track.Length], controller.BusAccess(0xC0EC));
        }
    }

    /// <summary>
    /// The timing contract the rest of the drive rests on: a byte takes 32 CPU cycles to pass
    /// under the head, so a read that arrives early gets "not ready" (bit 7 clear) and a poll loop
    /// waits for it.
    ///
    /// <para>This is worth a test of its own because getting it wrong does not fail loudly.
    /// Delivering a byte on every read instead — which an earlier model did — still boots every
    /// disk. What it breaks is DOS's drive-spinning check in RWTS, which reads the data register
    /// twice ~18 cycles apart and concludes the drive is stopped if the value never changes. With
    /// no time in the model that test degenerates into "are these 16 consecutive track bytes
    /// identical?", the window lands inside a run of sync bytes, and DOS takes its full one-second
    /// motor spin-up wait on every call: 87 of them in a System Master boot, 95 seconds instead of
    /// 12, with everything loading correctly the whole time.</para>
    /// </summary>
    [Fact]
    public void A_Byte_Takes_A_Byte_Time_To_Pass_Under_The_Head()
    {
        var controller = CreateEnabledController();

        _cycles += Disk2Controller.CyclesPerNibble;
        var first = controller.BusAccess(0xC0EC);
        Assert.True((first & 0x80) != 0, "A byte was due, so one must be delivered.");

        // Too soon — still shifting in.
        _cycles += Disk2Controller.CyclesPerNibble / 2;
        Assert.Equal(0x00, controller.BusAccess(0xC0EC));

        // A full byte time after the last delivery, the next one is there.
        _cycles += Disk2Controller.CyclesPerNibble;
        Assert.True((controller.BusAccess(0xC0EC) & 0x80) != 0);
    }

    [Fact]
    public void Write_Protect_Sense_Reports_Protected()
    {
        var controller = CreateEnabledController();

        controller.BusAccess(0xC0ED);           // Q6 on
        var status = controller.BusAccess(0xC0EE);   // Q7 off: sense
        Assert.True((status & 0x80) != 0, "Read-only emulation must report write-protected.");

        // Without Q6 set, $C0EE is just "select read mode".
        controller.BusAccess(0xC0EC);
        Assert.Equal(0x00, controller.BusAccess(0xC0EE));
    }

    [Fact]
    public void Stepper_Phases_Move_The_Head_By_Half_Tracks()
    {
        var controller = CreateEnabledController();
        Assert.Equal(0, controller.CurrentTrack);

        // Track 0 → 1: pulse phase 1 then phase 2 (on+off each), as RWTS does.
        controller.BusAccess(0xC0E3); controller.BusAccess(0xC0E2);
        controller.BusAccess(0xC0E5); controller.BusAccess(0xC0E4);
        Assert.Equal(1, controller.CurrentTrack);

        // Track 1 → 2: phases 3 then 0.
        controller.BusAccess(0xC0E7); controller.BusAccess(0xC0E6);
        controller.BusAccess(0xC0E1); controller.BusAccess(0xC0E0);
        Assert.Equal(2, controller.CurrentTrack);

        // And back down to 1: phases 3 then 2.
        controller.BusAccess(0xC0E7); controller.BusAccess(0xC0E6);
        controller.BusAccess(0xC0E5); controller.BusAccess(0xC0E4);
        Assert.Equal(1, controller.CurrentTrack);
    }

    [Fact]
    public void Recalibration_Clamps_At_Track_0()
    {
        var controller = CreateEnabledController();

        // Step up a few tracks first.
        controller.BusAccess(0xC0E3); controller.BusAccess(0xC0E5);
        controller.BusAccess(0xC0E7); controller.BusAccess(0xC0E1);
        Assert.Equal(2, controller.CurrentTrack);

        // The boot ROM's recalibration: many phase pulses walking downward.
        for (var i = 0; i < 40; i++)
        {
            var phase = 3 - (i % 4);
            controller.BusAccess((ushort)(0xC0E0 + (phase * 2) + 1));
            controller.BusAccess((ushort)(0xC0E0 + (phase * 2)));
        }
        Assert.Equal(0, controller.CurrentTrack);
    }

    [Fact]
    public void Head_Cannot_Step_Past_The_Last_Track()
    {
        var controller = CreateEnabledController();

        for (var i = 0; i < 200; i++)
        {
            var phase = (i % 4) + 1;
            controller.BusAccess((ushort)(0xC0E0 + ((phase & 3) * 2) + 1));
        }
        Assert.Equal(DskParser.Tracks - 1, controller.CurrentTrack);
    }

    [Fact]
    public void Stepping_Selects_The_Other_Tracks_Nibble_Stream()
    {
        var image = BuildDiskImage();
        var controller = CreateEnabledController();
        var track1 = Disk2TrackNibblizer.BuildNibbleTracks(image)[1];

        controller.BusAccess(0xC0E3);
        controller.BusAccess(0xC0E5);
        Assert.Equal(1, controller.CurrentTrack);

        // The head keeps its angular position across a seek, so the next byte time brings the
        // same point of the new track under it.
        _cycles += Disk2Controller.CyclesPerNibble;
        Assert.Equal(track1[2], controller.BusAccess(0xC0EC));
    }

    [Fact]
    public void Reset_Stops_The_Motor_But_Keeps_The_Head_Position()
    {
        var controller = CreateEnabledController();
        controller.BusAccess(0xC0E3);
        controller.BusAccess(0xC0E5);
        Assert.Equal(1, controller.CurrentTrack);
        Assert.True(controller.IsMotorOn);

        controller.Reset();

        Assert.False(controller.IsMotorOn);
        Assert.Equal(1, controller.SelectedDrive);
        Assert.Equal(1, controller.CurrentTrack);
    }

    /// <summary>
    /// Reads a whole sector the way RWTS does — scan for the address field of the wanted
    /// sector, then decode the following data field — proving the controller + nibblizer pair
    /// is consumable by real disk routines.
    /// </summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 15)]
    [InlineData(17, 3)]
    public void A_Software_Rwts_Can_Read_Any_Sector(int track, int physicalSector)
    {
        var image = BuildDiskImage();
        var controller = CreateEnabledController();

        // Seek: two phase pulses per track, starting from track 0.
        for (var t = 0; t < track; t++)
        {
            var halfTrack = t * 2;
            controller.BusAccess((ushort)(0xC0E0 + ((((halfTrack % 4) + 1) % 4) * 2) + 1));
            controller.BusAccess((ushort)(0xC0E0 + ((((halfTrack % 4) + 2) % 4) * 2) + 1));
        }
        Assert.Equal(track, controller.CurrentTrack);

        // Scan for the wanted sector's address field, then its data field.
        var found = false;
        var guard = Disk2TrackNibblizer.TrackSize * 3;
        while (guard-- > 0 && !found)
        {
            if (ReadNibble(controller) != 0xD5 || ReadNibble(controller) != 0xAA || ReadNibble(controller) != 0x96)
                continue;

            var addressField = new byte[8];
            for (var i = 0; i < addressField.Length; i++)
                addressField[i] = ReadNibble(controller);
            var (_, addressTrack, addressSector) = Disk2NibbleTestDecoder.DecodeAddressField(addressField);
            Assert.Equal(track, addressTrack);
            if (addressSector != physicalSector)
                continue;

            while (ReadNibble(controller) != 0xD5) { }
            Assert.Equal(0xAA, ReadNibble(controller));
            Assert.Equal(0xAD, ReadNibble(controller));

            var encoded = new byte[Disk2NibbleCodec.EncodedDataSize];
            for (var i = 0; i < encoded.Length; i++)
                encoded[i] = ReadNibble(controller);

            var decoded = Disk2NibbleTestDecoder.DecodeSector(encoded);
            var logical = Disk2TrackNibblizer.PhysicalToDosSector[physicalSector];
            var expected = image.AsSpan(DskParser.SectorOffset(track, logical), DskParser.BytesPerSector);
            Assert.Equal(expected.ToArray(), decoded);
            found = true;
        }

        Assert.True(found, $"Sector T{track} S{physicalSector} was never found in the stream.");
    }

    /// <summary>
    /// The Autostart ROM scans the slots for a bootable card only on a cold start, which it
    /// decides from the power-up byte at $03F4 (the complement of $03F3). Booting a disk on a
    /// machine that already reached the BASIC prompt therefore has to break that match first.
    /// </summary>
    [Fact]
    public void InvalidatePowerUpByte_Breaks_The_Warm_Start_Match()
    {
        var apple2 = new Apple2System();

        // Make the pair "valid", i.e. what a machine that has finished booting BASIC looks like.
        apple2.Mem[0x03F3] = 0x12;
        apple2.Mem[0x03F4] = (byte)(0x12 ^ 0xA5);

        apple2.InvalidatePowerUpByte();

        Assert.NotEqual((byte)(apple2.Mem[0x03F3] ^ 0xA5), apple2.Mem[0x03F4]);
    }

    [Fact]
    public void InvalidatePowerUpByte_Breaks_The_Match_For_Every_Soft_Entry_High_Byte()
    {
        var apple2 = new Apple2System();

        for (var high = 0; high <= 255; high++)
        {
            apple2.Mem[0x03F3] = (byte)high;
            apple2.Mem[0x03F4] = (byte)(high ^ 0xA5);

            apple2.InvalidatePowerUpByte();

            Assert.NotEqual((byte)(high ^ 0xA5), apple2.Mem[0x03F4]);
        }
    }

    [Fact]
    public void Apple2_System_Maps_The_Controller_Into_The_Address_Space()
    {
        var apple2 = new Apple2System();
        apple2.DiskController.SetBootRom(BuildBootRom());

        // Without a disk the slot looks empty on the bus.
        Assert.Equal(0xFF, apple2.Mem[0xC600]);
        apple2.Mem[0xC0E9] = 0;   // soft switches act on writes too — but only when enabled
        Assert.False(apple2.DiskController.IsMotorOn);

        apple2.DiskController.InsertDiskImage(BuildDiskImage());

        // Boot ROM visible at $C600 with the Autostart scan signature bytes.
        Assert.Equal(0xA2, apple2.Mem[0xC600]);
        Assert.Equal(0x20, apple2.Mem[0xC601]);
        Assert.Equal(0x00, apple2.Mem[0xC603]);
        Assert.Equal(0x03, apple2.Mem[0xC605]);
        Assert.Equal(0x3C, apple2.Mem[0xC607]);

        // Soft switches reachable through the memory map.
        _ = apple2.Mem[0xC0E9];
        Assert.True(apple2.DiskController.IsMotorOn);
        _ = apple2.Mem[0xC0E8];
        Assert.False(apple2.DiskController.IsMotorOn);
    }
}
