import { useMemo } from 'react';

interface VersionSpec {
  size: number;
  dataBytes: number;
  ecBytes: number;
  alignCoord: number;
}

// Single-block Level-L specs for Versions 2..5 (up to 106 UTF-8 bytes — plenty for UPI URIs)
const VERSION_SPECS: VersionSpec[] = [
  { size: 25, dataBytes: 34, ecBytes: 10, alignCoord: 18 },
  { size: 29, dataBytes: 55, ecBytes: 15, alignCoord: 22 },
  { size: 33, dataBytes: 80, ecBytes: 20, alignCoord: 26 },
  { size: 37, dataBytes: 108, ecBytes: 26, alignCoord: 30 },
];

const GF_EXP = new Uint8Array(512);
const GF_LOG = new Uint8Array(256);
(() => {
  let x = 1;
  for (let i = 0; i < 255; i++) {
    GF_EXP[i] = x;
    GF_LOG[x] = i;
    x <<= 1;
    if (x & 0x100) {
      x ^= 0x11d;
    }
  }
  for (let i = 255; i < 512; i++) {
    GF_EXP[i] = GF_EXP[i - 255];
  }
})();

function gfMul(a: number, b: number): number {
  if (a === 0 || b === 0) return 0;
  return GF_EXP[GF_LOG[a] + GF_LOG[b]];
}

function buildRsGenerator(degree: number): Uint8Array {
  let poly: Uint8Array<ArrayBufferLike> = new Uint8Array([1]);
  for (let i = 0; i < degree; i++) {
    const next = new Uint8Array(poly.length + 1);
    const root = GF_EXP[i];
    for (let j = 0; j < poly.length; j++) {
      next[j] ^= poly[j];
      next[j + 1] ^= gfMul(poly[j], root);
    }
    poly = next;
  }
  return poly;
}

function computeRsEc(data: Uint8Array, ecCount: number): Uint8Array {
  const gen = buildRsGenerator(ecCount);
  const work = new Uint8Array(data.length + ecCount);
  work.set(data, 0);
  for (let i = 0; i < data.length; i++) {
    const factor = work[i];
    if (factor !== 0) {
      for (let j = 0; j < gen.length; j++) {
        work[i + j] ^= gfMul(gen[j], factor);
      }
    }
  }
  return work.slice(data.length);
}

function encodeFormatInfo(maskId: number): number {
  // Error correction L = 01
  const data = (0b01 << 3) | maskId;
  let rem = data << 10;
  for (let i = 14; i >= 10; i--) {
    if (rem & (1 << i)) {
      rem ^= 0x537 << (i - 10);
    }
  }
  return ((data << 10) | rem) ^ 0x5412;
}

function buildQrMatrix(text: string): boolean[][] {
  const utf8 = new TextEncoder().encode(text);
  const spec =
    VERSION_SPECS.find((s) => utf8.length + 2 <= s.dataBytes) ??
    VERSION_SPECS[VERSION_SPECS.length - 1];

  const payload = utf8.slice(0, spec.dataBytes - 2);
  const bits: number[] = [];
  const pushBits = (val: number, len: number) => {
    for (let i = len - 1; i >= 0; i--) {
      bits.push((val >> i) & 1);
    }
  };

  pushBits(0b0100, 4); // Byte mode
  pushBits(payload.length, 8);
  for (const byte of payload) {
    pushBits(byte, 8);
  }
  const capacityBits = spec.dataBytes * 8;
  pushBits(0, Math.min(4, capacityBits - bits.length));
  while (bits.length % 8 !== 0) bits.push(0);
  const padBytes = [0xec, 0x11];
  let padIdx = 0;
  while (bits.length < capacityBits) {
    pushBits(padBytes[padIdx % 2], 8);
    padIdx++;
  }

  const dataBytes = new Uint8Array(spec.dataBytes);
  for (let i = 0; i < spec.dataBytes; i++) {
    let b = 0;
    for (let j = 0; j < 8; j++) {
      b = (b << 1) | bits[i * 8 + j];
    }
    dataBytes[i] = b;
  }

  const ecBytes = computeRsEc(dataBytes, spec.ecBytes);
  const allCodewords = new Uint8Array(dataBytes.length + ecBytes.length);
  allCodewords.set(dataBytes, 0);
  allCodewords.set(ecBytes, dataBytes.length);

  const n = spec.size;
  const grid: boolean[][] = Array.from({ length: n }, () => Array<boolean>(n).fill(false));
  const reserved: boolean[][] = Array.from({ length: n }, () => Array<boolean>(n).fill(false));

  const setModule = (r: number, c: number, dark: boolean, lock = true) => {
    if (r >= 0 && r < n && c >= 0 && c < n) {
      grid[r][c] = dark;
      if (lock) reserved[r][c] = true;
    }
  };

  const drawFinder = (top: number, left: number) => {
    for (let dr = -1; dr <= 7; dr++) {
      for (let dc = -1; dc <= 7; dc++) {
        const inBox = dr >= 0 && dr <= 6 && dc >= 0 && dc <= 6;
        const border = dr === 0 || dr === 6 || dc === 0 || dc === 6;
        const core = dr >= 2 && dr <= 4 && dc >= 2 && dc <= 4;
        setModule(top + dr, left + dc, inBox && (border || core));
      }
    }
  };

  drawFinder(0, 0);
  drawFinder(0, n - 7);
  drawFinder(n - 7, 0);

  // Alignment pattern (valid center for V2..V5)
  for (let dr = -2; dr <= 2; dr++) {
    for (let dc = -2; dc <= 2; dc++) {
      const dark = Math.max(Math.abs(dr), Math.abs(dc)) === 2 || (dr === 0 && dc === 0);
      setModule(spec.alignCoord + dr, spec.alignCoord + dc, dark);
    }
  }

  // Timing patterns
  for (let i = 8; i < n - 8; i++) {
    setModule(6, i, i % 2 === 0);
    setModule(i, 6, i % 2 === 0);
  }

  // Dark module
  setModule(n - 8, 8, true);

  // Reserve format info zones
  for (let i = 0; i <= 8; i++) {
    if (i !== 6) {
      reserved[8][i] = true;
      reserved[i][8] = true;
    }
  }
  for (let i = 0; i < 8; i++) {
    reserved[8][n - 1 - i] = true;
    reserved[n - 1 - i][8] = true;
  }

  // Place data + EC bits using standard zigzag
  let bitIndex = 0;
  const totalBits = allCodewords.length * 8;
  let goingUp = true;
  for (let rightCol = n - 1; rightCol >= 1; rightCol -= 2) {
    if (rightCol === 6) rightCol = 5;
    for (let step = 0; step < n; step++) {
      const r = goingUp ? n - 1 - step : step;
      for (let offset = 0; offset < 2; offset++) {
        const c = rightCol - offset;
        if (!reserved[r][c]) {
          let dark = false;
          if (bitIndex < totalBits) {
            const byteIdx = Math.floor(bitIndex / 8);
            const bitOffset = 7 - (bitIndex % 8);
            dark = ((allCodewords[byteIdx] >> bitOffset) & 1) === 1;
            bitIndex++;
          }
          grid[r][c] = dark;
        }
      }
    }
    goingUp = !goingUp;
  }

  // Apply Mask 0 ((r + c) % 2 === 0) and write Level L format bits
  const bestMask = 0;
  for (let r = 0; r < n; r++) {
    for (let c = 0; c < n; c++) {
      if (!reserved[r][c] && (r + c) % 2 === 0) {
        grid[r][c] = !grid[r][c];
      }
    }
  }

  const fmt = encodeFormatInfo(bestMask);
  for (let i = 0; i < 15; i++) {
    const bit = ((fmt >> i) & 1) === 1;
    // Horizontal
    if (i < 6) grid[8][i] = bit;
    else if (i === 6) grid[8][7] = bit;
    else if (i === 7) grid[8][8] = bit;
    else if (i === 8) grid[7][8] = bit;
    else grid[14 - i][8] = bit;

    // Vertical mirror
    if (i < 7) grid[n - 1 - i][8] = bit;
    else if (i === 7) grid[8][n - 8] = bit;
    else grid[8][n - 15 + i] = bit;
  }

  return grid;
}

interface QrMatrixSvgProps {
  value: string;
  sizePx?: number;
  label?: string;
  className?: string;
}

export function QrMatrixSvg({
  value,
  sizePx = 128,
  label = 'Dynamic UPI QR Code',
  className = '',
}: QrMatrixSvgProps) {
  const matrix = useMemo(() => buildQrMatrix(value || 'upi://pay?pa=gp@sbi&cu=INR'), [value]);
  const n = matrix.length;
  const quiet = 2;
  const viewSize = n + quiet * 2;

  return (
    <svg
      width={sizePx}
      height={sizePx}
      viewBox={`0 0 ${viewSize} ${viewSize}`}
      role="img"
      aria-label={label}
      className={`rounded-lg bg-white p-1 border border-slate-200 shadow-xs ${className}`}
      shapeRendering="crispEdges"
    >
      <rect width={viewSize} height={viewSize} fill="#ffffff" />
      {matrix.map((row, r) =>
        row.map((dark, c) =>
          dark ? (
            <rect key={`${r}-${c}`} x={c + quiet} y={r + quiet} width={1} height={1} fill="#0f172a" />
          ) : null,
        ),
      )}
    </svg>
  );
}

