# Loop through all the .asm files in all directories below the current 
# and build all .asm files with ACME,
# with cbm type, 
# and output to the Build subdirectory,
# and report file generated,
# and vice label file generated 

Push-Location

$ACME_APP = "c:\Users\highb\Documents\C64\ACME\acme.exe"

Get-ChildItem -Path . -Include *.asm -Recurse | ForEach-Object {
    $File = $_.FullName
    $Path = Split-Path -Path $File -Parent
    # Change current directory to the directory of the .asm file (to be able to resolve !binary includes in .asm files that is based on current directory, not the directory of the .asm file)
    Set-Location -Path $Path
    #$Name = Split-Path -Path $File -LeafBase
    $Name = [IO.Path]::GetFileNameWithoutExtension($File)
    $Output = Join-Path -Path $Path -ChildPath 'Build'
    New-Item -Path $Output -ItemType Directory -Force | Out-Null
    Write-Host "Building $Name.asm"
    & $ACME_APP -f cbm -o $Output\$Name.prg -r $Output\$Name.report --vicelabels $Output\$Name.labels $File
}

Pop-Location

# Apple II examples use the cc65 toolchain (ca65/ld65) and the .s extension, so the ACME
# loop above ignores them. ca65/ld65 are expected to be in PATH. The shared linker config
# Apple2/apple2-b.cfg emits a DOS 3.3 "B" file (4-byte load address + length header).
Push-Location

Get-ChildItem -Path . -Include *.s -Recurse | ForEach-Object {
    $File = $_.FullName
    $Path = Split-Path -Path $File -Parent
    Set-Location -Path $Path
    $Name = [IO.Path]::GetFileNameWithoutExtension($File)
    $Output = Join-Path -Path $Path -ChildPath 'Build'
    New-Item -Path $Output -ItemType Directory -Force | Out-Null
    Write-Host "Building $Name.s (ca65)"
    & ca65 $File -o (Join-Path $Output "$Name.o")
    & ld65 -C (Join-Path $Path '..' 'apple2-b.cfg') (Join-Path $Output "$Name.o") -o (Join-Path $Output "$Name.bin")
    Remove-Item (Join-Path $Output "$Name.o")
}

Pop-Location
