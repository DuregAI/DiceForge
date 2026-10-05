const fs = require('node:fs');
const zlib = require('node:zlib');
const path = require('node:path');
const build = path.join('Builds', 'WebGLBeta', 'Build');
const wasm = zlib.brotliDecompressSync(fs.readFileSync(path.join(build, 'WebGLBeta.wasm.br')));
const data = zlib.brotliDecompressSync(fs.readFileSync(path.join(build, 'WebGLBeta.data.br')));
const signature = Buffer.from('UnityWebData1.0\0');
if (!data.subarray(0, signature.length).equals(signature)) throw new Error('Unknown Unity data format');
const headerEnd = data.readUInt32LE(signature.length);
let cursor = signature.length + 4;
let metadata;
while (cursor < headerEnd) {
  const offset = data.readUInt32LE(cursor);
  const size = data.readUInt32LE(cursor + 4);
  const nameLength = data.readUInt32LE(cursor + 8);
  cursor += 12;
  if (cursor + nameLength > headerEnd || offset + size > data.length) throw new Error('Invalid archive entry');
  const name = data.subarray(cursor, cursor + nameLength).toString('utf8');
  cursor += nameLength;
  if (name.endsWith('global-metadata.dat')) metadata = data.subarray(offset, offset + size);
}
if (!metadata) throw new Error('IL2CPP metadata missing');
const result = {
  wasmBytes: wasm.length,
  dataBytes: data.length,
  validWasmMagic: wasm.subarray(0, 4).equals(Buffer.from([0, 97, 115, 109])),
  correctMovingHint: metadata.includes(Buffer.from('Moving\u2026', 'utf8')),
  corruptedMovingHint: metadata.includes(Buffer.from('Moving\u00e2\u20ac\u00a6', 'utf8')),
  russianFontLoadInMetadata: metadata.includes(Buffer.from('Localization/RobotoSlab')),
  chineseFontLoadInMetadata: metadata.includes(Buffer.from('Localization/NotoSansCJKsc-Regular')),
};
fs.writeFileSync(process.argv[2] || 'docs/Validation/BetaPass2/binary-audit.json', JSON.stringify(result, null, 2) + '\n');
console.log(JSON.stringify(result));
if (!result.validWasmMagic || !result.correctMovingHint || result.corruptedMovingHint || !result.russianFontLoadInMetadata || result.chineseFontLoadInMetadata) process.exitCode = 1;
