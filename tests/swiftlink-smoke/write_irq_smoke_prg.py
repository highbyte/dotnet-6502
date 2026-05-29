#!/usr/bin/env python3
import pathlib
import sys


LOAD_ADDRESS = 0xC000
COUNT_ADDR = 0xC100
DONE_ADDR = 0xC101
BUFFER_ADDR = 0xC200
EXPECTED_COUNT = 16
COMMAND_RECEIVE_IRQ_ENABLED = 0x09


class ProgramBuilder:
    def __init__(self, load_address: int):
        self.load_address = load_address
        self.code = bytearray()
        self.labels: dict[str, int] = {}
        self.absolute_patches: list[tuple[int, str]] = []
        self.relative_patches: list[tuple[int, str]] = []

    @property
    def current_address(self) -> int:
        return self.load_address + len(self.code)

    def label(self, name: str) -> None:
        self.labels[name] = self.current_address

    def byte(self, value: int) -> None:
        self.code.append(value & 0xFF)

    def bytes(self, *values: int) -> None:
        self.code.extend(value & 0xFF for value in values)

    def lda_imm(self, value: int) -> None:
        self.bytes(0xA9, value)

    def lda_abs(self, address: int) -> None:
        self.bytes(0xAD, address & 0xFF, (address >> 8) & 0xFF)

    def ldx_abs(self, address: int) -> None:
        self.bytes(0xAE, address & 0xFF, (address >> 8) & 0xFF)

    def sta_abs(self, address: int) -> None:
        self.bytes(0x8D, address & 0xFF, (address >> 8) & 0xFF)

    def stx_abs(self, address: int) -> None:
        self.bytes(0x8E, address & 0xFF, (address >> 8) & 0xFF)

    def cmp_imm(self, value: int) -> None:
        self.bytes(0xC9, value)

    def cpx_imm(self, value: int) -> None:
        self.bytes(0xE0, value)

    def and_imm(self, value: int) -> None:
        self.bytes(0x29, value)

    def branch(self, opcode: int, label: str) -> None:
        self.byte(opcode)
        self.relative_patches.append((len(self.code), label))
        self.byte(0x00)

    def bne(self, label: str) -> None:
        self.branch(0xD0, label)

    def beq(self, label: str) -> None:
        self.branch(0xF0, label)

    def bcs(self, label: str) -> None:
        self.branch(0xB0, label)

    def jmp(self, label: str) -> None:
        self.byte(0x4C)
        self.absolute_patches.append((len(self.code), label))
        self.bytes(0x00, 0x00)

    def patch_absolute(self, label: str) -> None:
        self.absolute_patches.append((len(self.code), label))
        self.bytes(0x00, 0x00)

    def build(self) -> bytes:
        for offset, label in self.absolute_patches:
            address = self.labels[label]
            self.code[offset] = address & 0xFF
            self.code[offset + 1] = (address >> 8) & 0xFF

        for offset, label in self.relative_patches:
            target = self.labels[label]
            branch_address = self.load_address + offset + 1
            delta = target - branch_address
            if delta < -128 or delta > 127:
                raise ValueError(f"Branch to {label} out of range: {delta}")
            self.code[offset] = delta & 0xFF

        return bytes(self.code)


def build_program() -> bytes:
    b = ProgramBuilder(LOAD_ADDRESS)

    b.bytes(0x78)  # sei
    b.lda_imm(0x35)
    b.bytes(0x85, 0x01)  # sta $01

    b.lda_imm(0x00)
    b.sta_abs(COUNT_ADDR)
    b.sta_abs(DONE_ADDR)

    b.lda_imm(COMMAND_RECEIVE_IRQ_ENABLED)
    b.sta_abs(0xDE02)
    b.lda_imm(0x00)
    b.sta_abs(0xDE03)

    irq_vector_low_immediate_offset = len(b.code) + 1
    b.lda_imm(0x00)
    b.sta_abs(0xFFFE)
    irq_vector_high_immediate_offset = len(b.code) + 1
    b.lda_imm(0x00)
    b.sta_abs(0xFFFF)

    b.bytes(0x58)  # cli

    b.label("main_loop")
    b.lda_abs(COUNT_ADDR)
    b.cmp_imm(EXPECTED_COUNT)
    b.bne("main_loop")
    b.lda_imm(0xAA)
    b.sta_abs(DONE_ADDR)
    b.jmp("done_loop")

    b.label("done_loop")
    b.jmp("done_loop")

    b.label("irq_handler")
    b.bytes(0x48)       # pha
    b.bytes(0x8A)       # txa
    b.bytes(0x48)       # pha
    b.bytes(0x98)       # tya
    b.bytes(0x48)       # pha
    b.lda_abs(0xDE01)   # ack status / inspect RX_FULL
    b.and_imm(0x08)
    b.beq("irq_restore")
    b.ldx_abs(COUNT_ADDR)
    b.cpx_imm(EXPECTED_COUNT)
    b.bcs("irq_drain_only")
    b.lda_abs(0xDE00)
    b.bytes(0x9D, BUFFER_ADDR & 0xFF, (BUFFER_ADDR >> 8) & 0xFF)  # sta buffer,x
    b.bytes(0xE8)       # inx
    b.stx_abs(COUNT_ADDR)
    b.jmp("irq_restore")

    b.label("irq_drain_only")
    b.lda_abs(0xDE00)

    b.label("irq_restore")
    b.bytes(0x68)  # pla
    b.bytes(0xA8)  # tay
    b.bytes(0x68)  # pla
    b.bytes(0xAA)  # tax
    b.bytes(0x68)  # pla
    b.bytes(0x40)  # rti

    program = bytearray(b.build())
    irq_handler_address = b.labels["irq_handler"]

    # Patch the IRQ vector immediates now that the handler address is known.
    program[irq_vector_low_immediate_offset] = irq_handler_address & 0xFF
    program[irq_vector_high_immediate_offset] = (irq_handler_address >> 8) & 0xFF

    return bytes([LOAD_ADDRESS & 0xFF, (LOAD_ADDRESS >> 8) & 0xFF]) + bytes(program)


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: write_irq_smoke_prg.py <output.prg>", file=sys.stderr)
        return 2

    output_path = pathlib.Path(sys.argv[1])
    output_path.write_bytes(build_program())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
