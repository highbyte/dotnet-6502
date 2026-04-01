Scripting:
- Expose more C64-specific functions to Lua scripting:
  - Set C64 config:
    - Check if config is valid, get list of validation errors.
    - If running on desktop:
      - Auto-download C64 ROMs to directory.
      - Get/set C64 ROM directory.
      - Get/set C64 ROM files.

Scripting on Browser
- Load & run a Lua script supplied in query parameter as a URL to download from? 
  - Show message to user if script source is trusted and maintain a whitelist in local storage?
- C64 exposed functions if running on browser:
    - Auto-download C64 ROMs to local storage
    - MAYBE:  Get or set C64 ROMs from browser local storage
