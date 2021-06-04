using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MidiController: MonoBehaviour {
    int C5Note = 72;
    string[] instruments = {
        "Piano", "Chromatic Percussion", "Organ", "Guitar", "Bass",
        "Strings", "Ensemble", "Brass", "Reed", "Pipe", "Synth Lead",
        "Synth Pad", "Synth Effects", "Ethnic", "Percussive", "Sound Effects"
    };

    // Start is called before the first frame update
    void Start() {
        string path = "Assets/Midis/stickerbush_symphony.mid";
        MidiFile midiFile = new MidiFile(path);

        print($"Format: {midiFile.Format}");
        print($"TicksPerQuarterNote: {midiFile.TicksPerQuarterNote}");
        print($"TracksCount: {midiFile.TracksCount}");
    }

    // Update is called once per frame
    void Update() {
        
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
