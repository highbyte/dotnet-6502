using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using Highbyte.DotNet6502.Systems.Recording;

namespace Highbyte.DotNet6502.Systems.Tests.Recording;

public class RecordingSessionTests
{
    [Theory]
    [InlineData(false, 50.125)]
    [InlineData(true, 59.94)]
    public void Shared_service_round_trips_states_and_ordered_events(bool events, double frameRate)
    {
        var source = new SyntheticAdapter(events, frameRate);
        using var snapshot = EmptySnapshot();
        using var file = new MemoryStream();
        using (var recording = new RecordingSession(source))
        {
            recording.StartRecording(file, snapshot, "Synthetic");
            for (int frame = 0; frame < 925; frame++)
            {
                source.Input = (byte)(frame / 125 % 4);
                Assert.True(recording.BeforeFrame());
                source.ExecuteFrame();
                recording.AfterFrame();
            }
            recording.StopRecording();
        }
        file.Position = 0;
        using var demo = new DemoReader(file);
        var target = new SyntheticAdapter(events, frameRate);
        using var playback = new RecordingSession(target);
        playback.StartPlayback(demo);
        while (playback.Mode == RecordingMode.Playback)
        {
            if (!playback.BeforeFrame())
                break;
            target.ExecuteFrame();
            playback.AfterFrame();
        }
        Assert.Equal(PlaybackOutcome.Completed, playback.Outcome);
        Assert.Equal(925, playback.Frame);
        Assert.Equal(source.State, target.State);
        Assert.Equal(1, target.ReleaseCount);
        Assert.Equal((byte)0, target.Input);
        using var timeline = demo.OpenTimeline();
        int runs = 0;
        while (timeline.Read() is { } record)
        {
            if (record.Input == null)
                continue;
            runs++;
            if (events)
                Assert.Equal(1u, record.Duration);
        }
        Assert.True(events ? runs == 925 : runs < 20);
    }

    [Fact]
    public void Interrupted_recording_has_no_completion_manifest()
    {
        using var file = new MemoryStream();
        using var snapshot = EmptySnapshot();
        var adapter = new SyntheticAdapter();
        using (var session = new RecordingSession(adapter))
        {
            session.StartRecording(file, snapshot);
            session.BeforeFrame();
            session.AfterFrame(completed: false);
            Assert.Equal(PlaybackOutcome.Failed, session.Outcome);
            Assert.Equal(0, session.Frame);
        }
        file.Position = 0;
        Assert.Throws<InvalidDataException>(() => new DemoReader(file));
    }

    [Fact]
    public void Divergence_reports_frame_and_releases_playback_input()
    {
        using var file = RecordOneFrame();
        using var demo = new DemoReader(file);
        var target = new SyntheticAdapter();
        using var session = new RecordingSession(target);
        session.StartPlayback(demo);
        session.BeforeFrame();
        target.State = 42;
        var error = Assert.Throws<InvalidDataException>(() => session.AfterFrame());
        Assert.Contains("frame 1", error.Message);
        Assert.Equal(RecordingMode.Idle, session.Mode);
        Assert.Equal(PlaybackOutcome.Failed, session.Outcome);
        Assert.True(session.CheckpointMismatch);
        Assert.Equal(1, target.ReleaseCount);
        Assert.False(session.BeforeFrame());
    }

    [Fact]
    public void Incompatible_adapter_is_rejected_before_input_is_applied()
    {
        using var file = RecordOneFrame();
        using var demo = new DemoReader(file);
        var target = new SyntheticAdapter(events: true);
        Assert.Throws<InvalidDataException>(() => demo.ValidateAdapter(target));
        Assert.Equal(0, target.State);
        Assert.Equal(0, target.ReleaseCount);
    }

    [Fact]
    public void Build_difference_is_advisory_but_machine_configuration_is_required()
    {
        using var file = RecordOneFrame();
        using var demo = new DemoReader(file);
        Assert.Equal(2, demo.Manifest.FormatVersion);
        Assert.False(string.IsNullOrWhiteSpace(demo.Manifest.RecordedBuildId));
        demo.Manifest.RecordedBuildId = "another-build";
        var adapter = new SyntheticAdapter();
        using var session = new RecordingSession(adapter);
        session.StartPlayback(demo);
        Assert.True(session.BeforeFrame());
        adapter.ExecuteFrame();
        session.AfterFrame();
        Assert.Equal(PlaybackOutcome.Completed, session.Outcome);

        demo.Manifest.CompatibilityId = "different-machine";
        Assert.Throws<InvalidDataException>(() => demo.ValidateAdapter(adapter));
    }

    [Fact]
    public void Legacy_demos_require_the_original_combined_hash()
    {
        using var file = RecordOneFrame();
        using var demo = new DemoReader(file);
        demo.Manifest.FormatVersion = 1;
        demo.Manifest.RecordedBuildId = null;
        var adapter = new SyntheticAdapter();
        Assert.Throws<InvalidDataException>(() => demo.ValidateAdapter(adapter));
        demo.Manifest.CompatibilityId = adapter.LegacyCompatibilityId;
        demo.ValidateAdapter(adapter);
    }

    [Fact]
    public void Initial_state_mismatch_is_rejected_before_playback()
    {
        using var file = RecordOneFrame();
        using var demo = new DemoReader(file);
        var target = new SyntheticAdapter { State = 1 };
        using var session = new RecordingSession(target);
        Assert.Throws<InvalidDataException>(() => session.StartPlayback(demo));
        Assert.Equal(RecordingMode.Idle, session.Mode);
    }

    [Fact]
    public void Tampered_payload_fails_integrity_validation()
    {
        using var file = RecordOneFrame();
        using (var archive = new ZipArchive(file, ZipArchiveMode.Update, leaveOpen: true))
        {
            archive.GetEntry("timeline.bin")!.Delete();
            using var replacement = archive.CreateEntry("timeline.bin").Open();
            replacement.Write([0, 1, 0, 0, 0, 0, 0, 0, 0]);
        }
        file.Position = 0;
        Assert.Throws<InvalidDataException>(() => new DemoReader(file));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("/absolute")]
    [InlineData("initial.d6502snap")]
    public void Unexpected_or_duplicate_archive_entries_are_rejected(string name)
    {
        using var file = RecordOneFrame();
        using (var archive = new ZipArchive(file, ZipArchiveMode.Update, leaveOpen: true))
            archive.CreateEntry(name);
        file.Position = 0;
        Assert.Throws<InvalidDataException>(() => new DemoReader(file));
    }

    [Theory]
    [InlineData(0u, 1)]
    [InlineData(1u, -1)]
    [InlineData(1u, 4097)]
    public void Timeline_rejects_invalid_duration_and_unbounded_payload(uint duration, int size)
    {
        using var file = new MemoryStream();
        using (var writer = new BinaryWriter(file, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((byte)0);
            writer.Write(duration);
            writer.Write(size);
        }
        file.Position = 0;
        using var cursor = new DemoTimelineCursor(file);
        Assert.Throws<InvalidDataException>(() => cursor.Read());
    }

    [Fact]
    public void Empty_recording_completes_without_executing_a_frame()
    {
        using var file = new MemoryStream();
        using var snapshot = EmptySnapshot();
        var adapter = new SyntheticAdapter();
        using (var session = new RecordingSession(adapter))
        {
            session.StartRecording(file, snapshot);
            session.StopRecording();
        }
        file.Position = 0;
        using var demo = new DemoReader(file);
        using var playback = new RecordingSession(adapter);
        playback.StartPlayback(demo);
        Assert.False(playback.BeforeFrame());
        Assert.Equal(PlaybackOutcome.Completed, playback.Outcome);
        Assert.Equal(0, playback.Frame);
    }

    private static MemoryStream RecordOneFrame()
    {
        var file = new MemoryStream();
        using var snapshot = EmptySnapshot();
        var adapter = new SyntheticAdapter { Input = 2 };
        using var session = new RecordingSession(adapter);
        session.StartRecording(file, snapshot);
        session.BeforeFrame();
        adapter.ExecuteFrame();
        session.AfterFrame();
        session.StopRecording();
        file.Position = 0;
        return file;
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void Zip_directory_cannot_lie_about_expanded_entry_length(int delta)
    {
        using var original = RecordOneFrame();
        var bytes = original.ToArray();
        for (int offset = 0; offset < bytes.Length - 46; offset++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)) != 0x02014b50)
                continue;
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 28));
            if (System.Text.Encoding.UTF8.GetString(bytes, offset + 46, nameLength) != "timeline.bin")
                continue;
            var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 24));
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + 24), (uint)(length + delta));
            using var tampered = new MemoryStream(bytes);
            Assert.Throws<InvalidDataException>(() => new DemoReader(tampered));
            return;
        }
        Assert.Fail("Timeline central directory entry was not found.");
    }

    [Fact]
    public void Disposing_an_active_recording_releases_ownership_and_leaves_an_incomplete_file()
    {
        using var file = new MemoryStream();
        using var snapshot = EmptySnapshot();
        var session = new RecordingSession(new SyntheticAdapter());
        session.StartRecording(file, snapshot);
        session.Dispose();
        Assert.Equal(RecordingMode.Idle, session.Mode);
        Assert.Equal(PlaybackOutcome.Failed, session.Outcome);
        file.Position = 0;
        Assert.Throws<InvalidDataException>(() => new DemoReader(file));
    }

    private static MemoryStream EmptySnapshot()
    {
        var file = new MemoryStream();
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
            archive.CreateEntry("synthetic-state.bin");
        file.Position = 0;
        return file;
    }

    private sealed class SyntheticAdapter(bool events = false, double frameRate = 50) : IRecordingAdapter
    {
        public string SystemName => "Synthetic";
        public string Schema => events ? "test-events" : "test-state";
        public int SchemaVersion => 1;
        public string CompatibilityId => "test-machine";
        public string LegacyCompatibilityId => "test-build-and-machine";
        public double FramesPerSecond => frameRate;
        public byte Input { get; set; }
        public long State { get; set; }
        public int ReleaseCount { get; private set; }
        public void ValidateSession() { }
        public void ValidateMemoryInspection(ushort address, int length) { }
        public byte[] CaptureInput() => events ? [Input, 7, 3] : [Input];
        public void ValidateInput(ReadOnlySpan<byte> input)
        {
            if (input.Length != (events ? 3 : 1) || input[0] > 3
                || (events && (input[1] != 7 || input[2] != 3)))
                throw new InvalidDataException("Invalid synthetic input.");
        }
        public bool CanRepeatInput(ReadOnlySpan<byte> input) => !events;
        public void ApplyInput(ReadOnlySpan<byte> input) => Input = input[0];
        public void ExecuteFrame() => State = checked(State * 3 % 1000003 + Input + (events ? 73 : 0));
        public byte[] ComputeStateDigest()
        {
            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(bytes, State);
            return SHA256.HashData(bytes);
        }
        public void ReleaseInput() { ReleaseCount++; Input = 0; }
    }
}
