import * as setup from '../test-setup';
import fs from 'fs';
import path from 'path';
import https from 'https';

export async function downloadC64Roms(romUrls: string[]) {
  for (const url of romUrls) {
    const filename = path.basename(url);
    const dest = path.join(setup.ARTIFACT_PATH, filename);
    await new Promise<void>((resolve, reject) => {
      const file = fs.createWriteStream(dest);
      const urlObj = new URL(url);
      const options = {
        hostname: urlObj.hostname,
        path: urlObj.pathname + urlObj.search,
        protocol: urlObj.protocol,
        headers: {
          'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
          'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8',
        },
      };
      https.get(options, (response) => {
        if (response.statusCode !== 200) {
          reject(new Error(`Failed to get '${url}' (${response.statusCode})`));
          return;
        }
        response.pipe(file);
        file.on('finish', () => file.close(() => resolve()));
      }).on('error', (err) => {
        fs.unlink(dest, () => reject(err));
      });
    });
    console.log(`Downloaded ${filename} to ${dest}`);
  }
}
