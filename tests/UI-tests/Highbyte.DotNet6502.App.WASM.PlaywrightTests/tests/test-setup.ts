import path from 'path';
import fs from 'fs';

export const BASE_URL = 'https://highbyte.se/dotnet-6502/app/';

export const ARTIFACT_PATH = path.join(__dirname, 'artifacts');
if (!fs.existsSync(ARTIFACT_PATH)) {
    fs.mkdirSync(ARTIFACT_PATH, { recursive: true });
}
