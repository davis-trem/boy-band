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
    struct Track {
        public AudioSource[] audioSources;
        public int audioSourceIndex;
        public AudioClip clip;
        public List<EventChunk> eventChunk;
        public int chuckIndex;
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

        tracks = new Track[1]; //[midiFile.TracksCount - 1];
        foreach (MidiTrack midiTrack in midiFile.Tracks.Skip(1).Take(1)) {
            Track track = new Track();
            track.audioSources = new AudioSource[] {
                gameObject.AddComponent<AudioSource>(),
                gameObject.AddComponent<AudioSource>()
            };
            track.audioSourceIndex = 0;
            track.eventChunk = new List<EventChunk>();
            track.chuckIndex = 0;
            EventChunk chunk = new EventChunk();
            chunk.events = new List<MidiEvent>();

            for (int i = 0; i < midiTrack.MidiEvents.Count; i++) {
                MidiEvent midiEvent = midiTrack.MidiEvents[i];
                // Group simultaneously events together
                if (i != 0 && midiEvent.Time != midiTrack.MidiEvents[i - 1].Time) {
                    // Add chuck of previous events
                    track.eventChunk.Add(chunk);
                    // Empty for new batch
                    chunk = new EventChunk();
                    chunk.events = new List<MidiEvent>();
                }
                chunk.time = midiEvent.Time;
                chunk.events.Add(midiEvent);
                // if it's the last chuck and it hasn't already been added to chunk
                if (i == midiTrack.MidiEvents.Count - 1 && midiEvent.Time == midiTrack.MidiEvents[i - 1].Time) {
                    track.eventChunk.Add(chunk);
                }
            }
            tracks[midiTrack.Index - 1] = track;
        }
        foreach (EventChunk ev in tracks[0].eventChunk) {
            print($"time: {ev.time}, length: {ev.events.Count}");
        }

        startTime = AudioSettings.dspTime + 0.2;
    }

    // Update is called once per frame
    void Update() {
        if (AudioSettings.dspTime < startTime) {
            return;
        }
        
    }

    void HandleMidiEvent(MidiEvent midiEvent, Track track) {
        switch (midiEvent.MidiEventType) {
            case MidiEventType.ProgramChange:
                
                break;
            case MidiEventType.ControlChange:
            case MidiEventType.NoteOn:
            case MidiEventType.NoteOff:
            case MidiEventType.ChannelAfterTouch:
            case MidiEventType.KeyAfterTouch:
            case MidiEventType.PitchBendChange:
                break;
        }
    }

    AudioClip GetSample(int midiNote, bool isPercussion) {
        AudioClip clip;
        if (isPercussion) {
            clip = Resources.Load($"Samples/Percussion/{midiNote}.wav") as AudioClip;
        } else {
            clip = Resources.Load($"Samples/{midiNote}_C5.wav") as AudioClip;
            if (clip == null) {
                int instrumentIndex = midiNote / 8;
                string instrument = instruments[instrumentIndex];
                for (int i = 8 * instrumentIndex; i < 8 * (instrumentIndex + 1); i++) {
                    clip = Resources.Load("Samples/" + i.ToString("000") + "_C5.wav") as AudioClip;
                    if (clip != null) {
                        break;
                    }
                }
            }
        }
        return clip;
    }
}
