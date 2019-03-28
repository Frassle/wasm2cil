﻿
open System
open System.IO
open System.Text
open System.Collections.Generic

open fsgen.opcodes

type Immediate =
    | I32
    | I64
    | F32
    | F64
    | U32
    | U8
    | MemArg
    | CallIndirect
    | BrTable
    | Nothing

let add_immediate (d: Dictionary<string,Immediate>) name im =
    d.Add(name, im)

let verify_immediates (d: Dictionary<string,Immediate>) =
    let h = 
        let h = HashSet<string>()
        for op in opcode_infos do
            h.Add(op.name) |> ignore
        h
    for s in d.Keys do
        if not (h.Contains(s)) then
            printfn "INVALID opcode in immediate lookup table: %s" s

let build_immediate_lookup () =
    let d = Dictionary<string,Immediate>()
    add_immediate d "I32Const" I32
    add_immediate d "I64Const" I64
    add_immediate d "F32Const" F32
    add_immediate d "F64Const" F64
    add_immediate d "Call" U32
    add_immediate d "Br" U32
    add_immediate d "BrIf" U32
    add_immediate d "LocalGet" U32
    add_immediate d "LocalSet" U32
    add_immediate d "LocalTee" U32
    add_immediate d "GlobalGet" U32
    add_immediate d "GlobalSet" U32
    add_immediate d "Block" U8
    add_immediate d "Loop" U8
    add_immediate d "If" U8
    add_immediate d "I32Load" MemArg
    add_immediate d "I64Load" MemArg
    add_immediate d "F32Load" MemArg
    add_immediate d "F64Load" MemArg
    add_immediate d "I32Load8S" MemArg
    add_immediate d "I32Load8U" MemArg
    add_immediate d "I32Load16S" MemArg
    add_immediate d "I32Load16U" MemArg
    add_immediate d "I64Load8S" MemArg
    add_immediate d "I64Load8U" MemArg
    add_immediate d "I64Load16S" MemArg
    add_immediate d "I64Load16U" MemArg
    add_immediate d "I64Load32S" MemArg
    add_immediate d "I64Load32U" MemArg
    add_immediate d "I32Store" MemArg
    add_immediate d "I64Store" MemArg
    add_immediate d "F32Store" MemArg
    add_immediate d "F64Store" MemArg
    add_immediate d "I32Store8" MemArg
    add_immediate d "I32Store16" MemArg
    add_immediate d "I64Store8" MemArg
    add_immediate d "I64Store16" MemArg
    add_immediate d "I64Store32" MemArg
    add_immediate d "MemorySize" U8
    add_immediate d "MemoryGrow" U8
    add_immediate d "CallIndirect" CallIndirect
    add_immediate d "BrTable" BrTable

    verify_immediates d

    d

let get_immediate (d: Dictionary<string,Immediate>) name =
    if (d.ContainsKey(name)) then
        d.[name]
    else
        Nothing

let get_prefixes () =
    let h = HashSet<int>()
    for op in opcode_infos do
        match op.prefix with
        | Some p -> h.Add(p) |> ignore
        | None -> ()
    h

let write_type_instruction path (immediates: Dictionary<string,Immediate>) =
    let sb = StringBuilder()
    let pr (s: string) =
        sb.Append(s + "\n") |> ignore
    "// this file is automatically generated" |> pr
    "module wasm.instr" |> pr
    "    open wasm.args" |> pr
    "    type Instruction =" |> pr
    for op in opcode_infos do
        match get_immediate immediates op.name with
        | I32 -> sprintf "        | %s of int32"  op.name |> pr
        | I64 -> sprintf "        | %s of int64"  op.name |> pr
        | F32 -> sprintf "        | %s of float32"  op.name |> pr
        | F64 -> sprintf "        | %s of double"  op.name |> pr
        | U32 -> sprintf "        | %s of uint32"  op.name |> pr
        | U8  -> sprintf "        | %s of byte"  op.name |> pr
        | MemArg  -> sprintf "        | %s of MemArg"  op.name |> pr
        | CallIndirect  -> sprintf "        | %s of CallIndirectArg"  op.name |> pr
        | BrTable  -> sprintf "        | %s of BrTableArg"  op.name |> pr
        | Nothing -> sprintf "        | %s"  op.name |> pr
    "\n" |> pr
    let txt = sb.ToString()
    File.WriteAllText(path, txt)
    
let write_function_read_instruction path (immediates: Dictionary<string,Immediate>) =
    let prefixes = get_prefixes()

    let sb = StringBuilder()
    let pr (s: string) =
        sb.Append(s + "\n") |> ignore
    "// this file is automatically generated" |> pr
    "module wasm.parse" |> pr
    "    open wasm.buffer" |> pr
    "    open wasm.instr" |> pr
    "    open wasm.args" |> pr

    pr "    let read_instruction (br: BinaryWasmStream) ="
    pr "        let b1 = br.ReadByte()"
    pr "        match b1 with"
    for n in prefixes do
        sprintf "        | 0x%02xuy ->" n |> pr
        sprintf "            let b2 = br.ReadByte()" |> pr
        sprintf "            match b2 with" |> pr
        for op in opcode_infos do
            match op.prefix with
            | Some x -> 
                if x = n then
                    sprintf "            | 0x%02xuy -> %s" op.code op.name |> pr
            | None -> ()
        sprintf "            | _      -> failwith \"todo\"" |> pr

    for op in opcode_infos do
        match op.prefix with
        | Some _ -> ()
        | None -> 
            match get_immediate immediates op.name with
            | I32 -> sprintf "        | 0x%02xuy -> %s (br.ReadVarInt32())" op.code op.name |> pr
            | I64 -> sprintf "        | 0x%02xuy -> %s (br.ReadVarInt64())" op.code op.name |> pr
            | F32 -> sprintf "        | 0x%02xuy -> %s (br.ReadFloat32())" op.code op.name |> pr
            | F64 -> sprintf "        | 0x%02xuy -> %s (br.ReadFloat64())" op.code op.name |> pr
            | U8 -> sprintf "        | 0x%02xuy -> %s (br.ReadByte())" op.code op.name |> pr
            | U32 -> sprintf "        | 0x%02xuy -> %s (br.ReadVarUInt32())" op.code op.name |> pr
            | MemArg -> sprintf "        | 0x%02xuy -> %s (read_memarg br)" op.code op.name |> pr
            | CallIndirect -> sprintf "        | 0x%02xuy -> %s (read_callindirect br)" op.code op.name |> pr
            | BrTable -> sprintf "        | 0x%02xuy -> %s (read_brtable br)" op.code op.name |> pr
            | Nothing -> sprintf "        | 0x%02xuy -> %s" op.code op.name |> pr
    sprintf "        | _      -> failwith \"todo\"" |> pr
    sprintf "" |> pr

    let txt = sb.ToString()
    File.WriteAllText(path, txt)
    
[<EntryPoint>]
let main argv =
    let dir_top =
        let cwd = Directory.GetCurrentDirectory()
        Path.GetFullPath(Path.Combine(cwd, ".."))
        // TODO or maybe walk upward until we find the right directory

    let dir_wasm = Path.Combine(dir_top, "wasm")
    let immediates = build_immediate_lookup ()
    write_type_instruction (Path.Combine(dir_wasm, "Instruction.fs")) immediates
    write_function_read_instruction (Path.Combine(dir_wasm, "ReadInstruction.fs")) immediates

    0 // return an integer exit code

