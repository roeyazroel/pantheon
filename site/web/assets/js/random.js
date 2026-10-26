// The game's seeded randomness, ported from C# so the site draws exactly what the game draws
// (ADR-076): FNV-1a for text seeds and stream names (StableHash), SplitMix64 to mix sub-seeds
// (SeedMixer), and xoshiro256** for the streams themselves (SimulationRandom). 64-bit arithmetic uses
// BigInt; every value is kept to 64 bits as the C# ulong is.

const MASK = (1n << 64n) - 1n;
const FNV_OFFSET = 14695981039346656037n;
const FNV_PRIME = 1099511628211n;
const UTF8 = new TextEncoder();

/** The 64-bit FNV-1a hash of a string's UTF-8 bytes (StableHash.Fnv1a64). */
export function fnv1a64(text) {
  let hash = FNV_OFFSET;
  for (const byte of UTF8.encode(text)) {
    hash ^= BigInt(byte);
    hash = (hash * FNV_PRIME) & MASK;
  }
  return hash;
}

/**
 * A seed from what a player types (WorldSeed.Parse): a decimal number that fits 64 bits is used as
 * it is; any other text is hashed. Returns null for empty text.
 */
export function parseSeed(text) {
  const trimmed = String(text).trim();
  if (trimmed.length === 0) {
    return null;
  }
  if (/^[0-9]+$/.test(trimmed)) {
    const value = BigInt(trimmed);
    if (value <= MASK) {
      return value;
    }
  }
  return fnv1a64(trimmed);
}

/** SplitMix64's finaliser (SeedMixer.Mix). */
function mix(value) {
  let z = value & MASK;
  z = ((z ^ (z >> 30n)) * 0xbf58476d1ce4e5b9n) & MASK;
  z = ((z ^ (z >> 27n)) * 0x94d049bb133111ebn) & MASK;
  return z ^ (z >> 31n);
}

/** A sub-seed for a purpose, and optionally a step of it (SeedMixer.Derive). */
export function derive(seed, purpose, counter) {
  const base = mix(seed ^ mix(fnv1a64(purpose)));
  return counter === undefined ? base : mix(base ^ mix(BigInt.asUintN(64, BigInt(counter))));
}

function rotl(value, shift) {
  const s = BigInt(shift);
  return ((value << s) | (value >> (64n - s))) & MASK;
}

/** A xoshiro256** stream, its state expanded from one seed with SplitMix64 (SimulationRandom). */
export class SimulationRandom {
  constructor(seed) {
    let state = seed & MASK;
    const next = () => {
      state = (state + 0x9e3779b97f4a7c15n) & MASK;
      let z = state;
      z = ((z ^ (z >> 30n)) * 0xbf58476d1ce4e5b9n) & MASK;
      z = ((z ^ (z >> 27n)) * 0x94d049bb133111ebn) & MASK;
      return z ^ (z >> 31n);
    };
    this.s0 = next();
    this.s1 = next();
    this.s2 = next();
    this.s3 = next();
  }

  /** The next 64 random bits. */
  nextUInt64() {
    const result = (rotl((this.s1 * 5n) & MASK, 7) * 9n) & MASK;
    const shifted = (this.s1 << 17n) & MASK;
    this.s2 ^= this.s0;
    this.s3 ^= this.s1;
    this.s1 ^= this.s2;
    this.s0 ^= this.s3;
    this.s2 ^= shifted;
    this.s3 = rotl(this.s3, 45);
    return result;
  }

  /** An unbiased integer from 0 up to, not including, a bound (Lemire's method, as NextInt). */
  nextInt(maxExclusive) {
    if (!(maxExclusive > 0)) {
      throw new RangeError('The bound must be positive.');
    }
    const bound = BigInt(maxExclusive);
    const threshold = ((MASK + 1n) - bound) % bound;
    for (;;) {
      const product = this.nextUInt64() * bound;
      if ((product & MASK) >= threshold) {
        return Number(product >> 64n);
      }
    }
  }

  /** A float from 0 up to 1, with 24 bits as the game's float has (NextFloat). */
  nextFloat() {
    return Number(this.nextUInt64() >> 40n) / 16777216;
  }
}

/** Rounds half to even, as .NET's MathF.Round does by default. */
export function roundHalfEven(value) {
  const floor = Math.floor(value);
  const fraction = value - floor;
  if (fraction > 0.5) {
    return floor + 1;
  }
  if (fraction < 0.5) {
    return floor;
  }
  return floor % 2 === 0 ? floor : floor + 1;
}
