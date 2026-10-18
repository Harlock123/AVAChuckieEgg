#!/usr/bin/env python3
"""Synthesises AVAChuckieEgg's sound effects and tunes into ../src/AVAChuckieEgg/Sounds as 16-bit mono WAV.

Nothing is sampled from any game: every effect (the clucks and quacks too) is built here from square, triangle, saw and
noise voices. About the henhouse plays The Irish Washerwoman (a traditional jig); the title's tune is a new composition.
Harry's one word is spoken by the formant voice in speech.py. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

from speech import say



RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVAChuckieEgg", "Sounds")
RNG = random.Random(1984)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def seq(bpm, notes, kind="pulse", gain=0.55, release=0.6):
    beat = 60.0 / bpm
    out = []
    for note, beats in notes:
        d = beats * beat
        out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=kind, release=release, gain=gain))
    return out




def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith(".wav"):
            os.remove(os.path.join(OUT, f))
    write("Jump.wav", tone(0.14, 500, 900, "square", attack=0.002, release=0.5, gain=0.5), 0.12)
    write("Land.wav", tone(0.04, 180, 90, "square", attack=0.001, release=1.2, gain=0.5), 0.1)
    write("Egg.wav", cat(*[tone(0.04, hz(n), kind="square", release=0.4) for n in ["C6", "E6", "G6"]]), 0.2)
    write("Grain.wav", cat(*[tone(0.05, hz(n), kind="tri", release=0.4) for n in ["G5", "D5", "G5"]]), 0.22)
    write("Die.wav", cat(*[tone(0.035, 1500 - i * 55, kind="square", release=0.2, gain=0.6) for i in range(24)]), 0.32)
    write("Tick.wav", tone(0.025, 1800, 1200, "square", attack=0.001, release=1.0, gain=0.5), 0.12)
    # "Egg-cellent!" when the henhouse is cleared.
    words = "EH1 G S , AH L AH N T !"
    write("SayDone.wav", mix(say(words, rate=RATE, pitch=150, speed=1.05, size=0.95), [s * 0.3 for s in say(words, rate=RATE, pitch=225, speed=1.05, size=0.95, seed=11)]), 0.5)
    write("Done.wav", cat(*[tone(0.06, hz(n), kind="pulse", release=0.3) for n in ["G5", "B5", "D6", "G6", "B6", "D7"]], tone(0.3, hz("G7"), kind="pulse", vibrato=0.02, release=0.5)), 0.28)
    # A hen's cluck: short bursts falling in pitch; the duck's quack: a nasal buzz.
    write("Cluck.wav", cat(*[cat(tone(0.05, 900 - k * 80, 500, "saw", attack=0.002, release=0.6, gain=0.6), silence(0.04)) for k in range(3)]), 0.18)
    write("Quack.wav", mix(tone(0.22, 420, 300, "saw", attack=0.005, release=0.5, vibrato=0.05, vib_rate=40), tone(0.22, 840, 600, "pulse", attack=0.005, release=0.6, gain=0.4)), 0.24)

    # About the henhouse: The Irish Washerwoman (traditional jig), with an oom-pah under it.
    t = 1 / 3
    jig = [("B4", t), ("G4", t), ("G4", t), ("D4", t), ("G4", t), ("G4", t), ("B4", t), ("G4", t), ("B4", t), ("D5", t), ("C5", t), ("B4", t),
           ("C5", t), ("A4", t), ("A4", t), ("E4", t), ("A4", t), ("A4", t), ("C5", t), ("A4", t), ("C5", t), ("E5", t), ("D5", t), ("C5", t),
           ("B4", t), ("G4", t), ("G4", t), ("D4", t), ("G4", t), ("G4", t), ("B4", t), ("G4", t), ("B4", t), ("D5", t), ("C5", t), ("B4", t),
           ("C5", t), ("B4", t), ("C5", t), ("A4", t), ("D5", t), ("C5", t), ("B4", t), ("G4", t), ("G4", t), ("G4", 3 * t)]
    def bar(root, fifth):
        return [(root, 1), (fifth, 1)]
    bass = bar("G2", "D3") * 2 + bar("A2", "E3") * 2 + bar("G2", "D3") * 2 + bar("A2", "D3") + bar("G2", "D3")
    write("Music.wav", cat(tune(116, jig, bass, kind="pulse"), tune(116, [(n[:-1] + str(int(n[-1]) + 1), b) for n, b in jig], bass, kind="pulse")), 0.26)
    # The title: a barnyard tune of its own.
    lead = [("C5", 0.5), ("E5", 0.5), ("G5", 1), ("E5", 0.5), ("C5", 0.5), ("D5", 1), ("F5", 0.5), ("D5", 0.5), ("B4", 1), ("G4", 1),
            ("C5", 0.5), ("E5", 0.5), ("G5", 1), ("A5", 0.5), ("G5", 0.5), ("E5", 1), ("D5", 0.5), ("B4", 0.5), ("C5", 2)]
    tbass = [("C3", 1), ("G2", 1), ("C3", 1), ("G2", 1), ("G2", 1), ("D3", 1), ("G2", 1), ("D3", 1), ("C3", 1), ("G2", 1), ("C3", 1), ("E3", 1), ("G2", 1), ("G2", 1), ("C3", 2)]
    write("TitleTune.wav", cat(tune(132, lead, tbass, kind="pulse"), silence(1.0)), 0.32)
    write("Victory.wav", tune(150, [("G5", 0.5), ("B5", 0.5), ("D6", 0.5), ("G6", 1.5), ("F#6", 0.5), ("G6", 0.5), ("A6", 0.5), ("B6", 3)],
                              [("G3", 1), ("D3", 1), ("E3", 1), ("C3", 1), ("D3", 1), ("G3", 2.5)], kind="pulse"), 0.4)
    write("GameOver.wav", tune(70, [("E5", 1), ("C5", 1), ("A4", 1), ("E4", 1), ("A4", 3)], [("A2", 2), ("F2", 2), ("A1", 3)], kind="tri"), 0.42)


if __name__ == "__main__":
    main()
