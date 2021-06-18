using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MidiController: MonoBehaviour {
    int C5Note = 72;
    string[] instruments = {
        "Piano", "Chromatic Percussion", "Organ", "Guitar", "Bass",
        "Strings", "Ensemble", "Brass", "Reed", "Pipe", "Synth Lead",
        "Synth Pad", "Synth Effects", "Ethnic", "Percussive", "Sound Effects"
    };

    struct EventChunk {
        public int time;
        public List<MidiEvent> events;
    };
    struct AudioEvent {
        public int note;
        public int velocity;
        public int startTime;
        public int endTime;
        public int audioSourceListIndex;
    }
    struct TrackAudioSource {
        public AudioSource[] audioSources;
        public int audioSourceIndex;
    }
    struct Track {
        public List<TrackAudioSource> audioSourceList;
        public AudioSource[] audioSources;
        public int audioSourceIndex;
        public AudioClip clip;
        public List<EventChunk> eventChunks;
        public int chunkIndex;
        public List<AudioEvent> audioEvents;
        public int audioEventIndex;
        public int maxVolume;
        public int expression;
        public bool isPercussion;
    };
    Track[] tracks;
    
    double ticksPerSecond; // (tick / ticksPerSecond) to get absolute time

    double startTime;

    // Start is called before the first frame update
    void Start() {
        string path = "Assets/Midis/stickerbush_symphony.mid";
        MidiFile midiFile = new MidiFile(path);

        print($"Format: {midiFile.Format}");
        print($"TicksPerQuarterNote: {midiFile.TicksPerQuarterNote}");
        print($"TracksCount: {midiFile.TracksCount}");

        // Get meta info from Track 0
        int bpm = 100;
        foreach (MidiEvent midiEvent in midiFile.Tracks[0].MidiEvents) {
            switch (midiEvent.MetaEventType) {
                case MetaEventType.Tempo:
                    bpm = midiEvent.Arg2;
                    break;
                case MetaEventType.TimeSignature:
                case MetaEventType.KeySignature:
                    break;
            }
        }
        print($"BPM: {bpm}");

        double ticksPerMinute = bpm * midiFile.TicksPerQuarterNote;
        ticksPerSecond = ticksPerMinute / 60d;

        // tracks = new Track[1];
        tracks = new Track[midiFile.TracksCount - 1];
        // foreach (MidiTrack midiTrack in midiFile.Tracks.Skip(1).Take(1)) {
        foreach (MidiTrack midiTrack in midiFile.Tracks.Skip(1)) {
            Track track = new Track();
            track.maxVolume = 127; // 127 is the highest value but may change during an event
            track.expression = 127;
            track.isPercussion = midiTrack.Index == 10;
            track.audioSources = new AudioSource[] {
                gameObject.AddComponent<AudioSource>(),
                gameObject.AddComponent<AudioSource>()
            };
            track.audioSourceIndex = 0;
            track.eventChunks = new List<EventChunk>();
            track.chunkIndex = 0;
            EventChunk chunk = new EventChunk();
            chunk.events = new List<MidiEvent>();

            track.audioEvents = new List<AudioEvent>();
            track.audioEventIndex = 0;
            Dictionary<int, AudioEvent> pendingAudioEvents = new Dictionary<int, AudioEvent>();

            track.audioSourceList = new List<TrackAudioSource>();
            Dictionary<int, int> simultaneouslyNotes = new Dictionary<int, int>(); // <Note, time>

            for (int i = 0; i < midiTrack.MidiEvents.Count; i++) {
                MidiEvent midiEvent = midiTrack.MidiEvents[i];
                // Group simultaneously events together
                if (i != 0 && midiEvent.Time != midiTrack.MidiEvents[i - 1].Time) {
                    // Add chuck of previous events
                    track.eventChunks.Add(chunk);
                    // Empty for new batch
                    chunk = new EventChunk();
                    chunk.events = new List<MidiEvent>();
                }
                chunk.time = midiEvent.Time;
                chunk.events.Add(midiEvent);
                // if it's the last chuck and it hasn't already been added to chunk
                if (i == midiTrack.MidiEvents.Count - 1 && midiEvent.Time == midiTrack.MidiEvents[i - 1].Time) {
                    track.eventChunks.Add(chunk);
                }

                // Audio Events Work
                if (midiEvent.MidiEventType == MidiEventType.NoteOn) {
                    simultaneouslyNotes.Add(midiEvent.Note, midiEvent.Time);
                    if (simultaneouslyNotes.Count > track.audioSourceList.Count) {
                        TrackAudioSource tas = new TrackAudioSource();
                        tas.audioSources = new AudioSource[] {
                            gameObject.AddComponent<AudioSource>(),
                            gameObject.AddComponent<AudioSource>()
                        };
                        tas.audioSourceIndex = 0;
                        track.audioSourceList.Add(tas);
                    }

                    AudioEvent audioEvent = new AudioEvent();
                    audioEvent.note = midiEvent.Note;
                    audioEvent.velocity = midiEvent.Velocity;
                    audioEvent.startTime = midiEvent.Time;
                    audioEvent.audioSourceListIndex = simultaneouslyNotes.Count - 1;
                    pendingAudioEvents.Add(midiEvent.Note, audioEvent);
                }
                if (midiEvent.MidiEventType == MidiEventType.NoteOff) {
                    simultaneouslyNotes.Remove(midiEvent.Note);

                    AudioEvent audioEvent = pendingAudioEvents[midiEvent.Note];
                    audioEvent.endTime = midiEvent.Time;
                    track.audioEvents.Add(audioEvent);
                    pendingAudioEvents.Remove(midiEvent.Note);
                }
            }
            tracks[midiTrack.Index - 1] = track;
        }
        // foreach (EventChunk ev in tracks[0].eventChunks) {
        //     print($"time: {ev.time}, length: {ev.events.Count}");
        // }
        // foreach (AudioEvent av in tracks[0].audioEvents) {
        //     print($"note: {av.note}, st: {av.startTime}, et: {av.endTime}");
        // }

        print($"dsp: {AudioSettings.dspTime}");
        startTime = AudioSettings.dspTime + 2;
    }

    // Update is called once per frame
    void Update() {
        for (int i = 0; i < tracks.Length; i++) {
            if (tracks[i].isPercussion) {
                continue;
            }
            int chunkIndex = tracks[i].chunkIndex;
            if (
                tracks[i].eventChunks.Count != chunkIndex + 1 &&
                AudioSettings.dspTime >= CalcDspTime(tracks[i].eventChunks[chunkIndex].time)
            ) {
                HandleMidiEvents(tracks[i].eventChunks[chunkIndex].events, i);
                tracks[i].chunkIndex++;
            }

            // Audio Events Work
            if (tracks[i].audioEventIndex == 0) {
                // ScheduleNote(tracks[i].audioSources[0], tracks[i].audioEvents[0], tracks[i].maxVolume);
                // ScheduleNote(tracks[i].audioSources[1], tracks[i].audioEvents[1], tracks[i].maxVolume);
                AudioEvent audioEvent = tracks[i].audioEvents[0];
                TrackAudioSource tas = tracks[i].audioSourceList[audioEvent.audioSourceListIndex];
                ScheduleNote(
                    tas.audioSources[tas.audioSourceIndex],
                    audioEvent,
                    tracks[i].maxVolume
                );
                tas.audioSourceIndex = 1 - tas.audioSourceIndex;
                tracks[i].audioSourceList[audioEvent.audioSourceListIndex] = tas;

                audioEvent = tracks[i].audioEvents[1];
                tas = tracks[i].audioSourceList[audioEvent.audioSourceListIndex];
                ScheduleNote(
                    tas.audioSources[tas.audioSourceIndex],
                    audioEvent,
                    tracks[i].maxVolume
                );
                tas.audioSourceIndex = 1 - tas.audioSourceIndex;
                tracks[i].audioSourceList[audioEvent.audioSourceListIndex] = tas;

                tracks[i].audioEventIndex = 1;
            } else {
                int audioEventIndex = tracks[i].audioEventIndex;
                if (
                    tracks[i].audioEvents.Count != audioEventIndex + 1 &&
                    AudioSettings.dspTime >= CalcDspTime(tracks[i].audioEvents[audioEventIndex].startTime)
                ) {
                    int audioSourceIndex = tracks[i].audioSourceIndex;
                    AudioEvent audioEvent = tracks[i].audioEvents[audioEventIndex + 1];
                    // ScheduleNote(tracks[i].audioSources[audioSourceIndex], audioEvent, tracks[i].maxVolume);
                    TrackAudioSource tas = tracks[i].audioSourceList[audioEvent.audioSourceListIndex];
                    ScheduleNote(
                        tas.audioSources[tas.audioSourceIndex],
                        audioEvent,
                        tracks[i].maxVolume
                    );
                    tas.audioSourceIndex = 1 - tas.audioSourceIndex;
                    tracks[i].audioSourceList[audioEvent.audioSourceListIndex] = tas;

                    tracks[i].audioEventIndex++;
                    // tracks[i].audioSourceIndex = 1 - audioSourceIndex;
                }
            }
        }
    }

    double CalcDspTime(int time) {
        return startTime + (time / ticksPerSecond);
    }

    void ScheduleNote(AudioSource audioSource, AudioEvent audioEvent, int maxVolume) {
        audioSource.PlayScheduled(CalcDspTime(audioEvent.startTime));
        audioSource.SetScheduledEndTime(CalcDspTime(audioEvent.endTime));
        audioSource.pitch = Mathf.Pow(1.05946f, audioEvent.note - C5Note);
    }

    void HandleMidiEvents(List<MidiEvent> events, int trackIndex) {
        foreach (MidiEvent midiEvent in events) {
            switch (midiEvent.MidiEventType) {
                case MidiEventType.ProgramChange:
                    tracks[trackIndex].clip = GetSample(midiEvent.Arg2, tracks[trackIndex].isPercussion);
                    // tracks[trackIndex].audioSources[0].clip = tracks[trackIndex].clip;
                    // tracks[trackIndex].audioSources[1].clip = tracks[trackIndex].clip;
                    foreach (TrackAudioSource tas in tracks[trackIndex].audioSourceList) {
                        tas.audioSources[0].clip = tracks[trackIndex].clip;
                        tas.audioSources[1].clip = tracks[trackIndex].clip;
                    }
                    break;
                case MidiEventType.ControlChange:
                    switch (midiEvent.ControlChangeType) {
                        case ControlChangeType.Volume:
                            tracks[trackIndex].maxVolume = midiEvent.Arg3;
                            break;
                        case ControlChangeType.Expression:
                            tracks[trackIndex].expression = midiEvent.Arg3;
                            break;
                        case ControlChangeType.BankSelect:
                        case ControlChangeType.Balance:
                        case ControlChangeType.Modulation:
                        case ControlChangeType.Pan:
                        case ControlChangeType.Sustain:
                            break;
                    }
                    break;
                case MidiEventType.NoteOn:
                    // 127 is the highest value
                    // tracks[trackIndex].audioSources[0].volume = ((((midiEvent.Velocity / 127f) * tracks[trackIndex].expression) / 127f) * tracks[trackIndex].maxVolume) / 127f;
                    // tracks[trackIndex].audioSources[1].volume = ((((midiEvent.Velocity / 127f) * tracks[trackIndex].expression) / 127f) * tracks[trackIndex].maxVolume) / 127f;
                    foreach (TrackAudioSource tas in tracks[trackIndex].audioSourceList) {
                        tas.audioSources[0].volume = ((((midiEvent.Velocity / 127f) * tracks[trackIndex].expression) / 127f) * tracks[trackIndex].maxVolume) / 127f;
                        tas.audioSources[1].volume = ((((midiEvent.Velocity / 127f) * tracks[trackIndex].expression) / 127f) * tracks[trackIndex].maxVolume) / 127f;
                    }
                    break;
                case MidiEventType.NoteOff:
                case MidiEventType.ChannelAfterTouch:
                case MidiEventType.KeyAfterTouch:
                case MidiEventType.PitchBendChange:
                    break;
            }
        }
        
    }

    AudioClip GetSample(int midiNote, bool isPercussion) {
        AudioClip clip;

        if (isPercussion) {
            clip = Resources.Load($"Samples/Percussion/{midiNote}") as AudioClip;
        } else {
            clip = Resources.Load($"Samples/{midiNote}_C5") as AudioClip;
            if (clip == null) {
                int instrumentIndex = midiNote / 8;
                string instrument = instruments[instrumentIndex];
                for (int i = 8 * instrumentIndex; i < 8 * (instrumentIndex + 1); i++) {
                    clip = Resources.Load("Samples/" + i.ToString("000") + "_C5") as AudioClip;
                    if (clip != null) {
                        break;
                    }
                }
            }
        }
        return clip;
    }
}
