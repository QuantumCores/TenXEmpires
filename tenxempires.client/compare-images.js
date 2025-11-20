import fs from 'fs';
import { PNG } from 'pngjs';
import pixelmatch from 'pixelmatch';

const args = process.argv.slice(2);
if (args.length < 2) {
  console.error('Usage: node compare-images.js <path-to-image1> <path-to-image2>');
  process.exit(1);
}

const img1Path = args[0];
const img2Path = args[1];

try {
  const img1 = PNG.sync.read(fs.readFileSync(img1Path));
  const img2 = PNG.sync.read(fs.readFileSync(img2Path));

  const { width, height } = img1;
  
  if (img2.width !== width || img2.height !== height) {
    console.error(`Image dimensions do not match: ${width}x${height} vs ${img2.width}x${img2.height}`);
    process.exit(1);
  }

  const diff = new PNG({ width, height });
  const numDiffPixels = pixelmatch(img1.data, img2.data, diff.data, width, height, { threshold: 0.1 });

  console.log(`\n----------------------------------------`);
  console.log(`Image 1: ${img1Path}`);
  console.log(`Image 2: ${img2Path}`);
  console.log(`Dimensions: ${width}x${height}`);
  console.log(`Different pixels: ${numDiffPixels}`);
  console.log(`Percentage: ${((numDiffPixels / (width * height)) * 100).toFixed(4)}%`);
  console.log(`----------------------------------------\n`);

} catch (error) {
  console.error('Error comparing images:', error.message);
}

