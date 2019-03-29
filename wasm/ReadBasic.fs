
module wasm.read_basic

    type BinaryWasmStream(ba: byte[]) =
        let buf = ba
        let mutable offset = 0

        let read_byte () =
            let b = buf.[offset]
            offset <- offset + 1
            b

        let rec read_var_signed count (value: int64) =
            let b = read_byte ()
            let result = value ||| (int64 (b &&& 0x7Fuy) <<< (count * 7))
            if (b &&& 0x80uy) = 0uy then
                let sign = -1L <<< ((count + 1) * 7)
                if ((sign >>> 1) &&& result) <> 0L then
                    result ||| sign
                else
                    result
            else
                read_var_signed (count + 1) result

        let rec read_var_unsigned count (value: uint32) =
            let b = read_byte ()
            let result = value ||| (uint32 (b &&& 0x7Fuy) <<< (count * 7))
            if (b &&& 0x80uy) = 0uy then
                result
            else
                read_var_unsigned (count + 1) result

        member this.ReadByte() =
            let b = buf.[offset]
            offset <- offset + 1
            b

        member this.Remaining() =
            buf.Length - offset

        member this.Length() =
            buf.Length

        member this.ReadVarUInt32() =
            read_var_unsigned 0 0u

        member this.ReadVarInt32() =
            read_var_signed 0 0L |> int32

        member this.ReadVarInt64() =
            read_var_signed 0 0L

        member this.ReadBytes(len: uint32) =
            let last = offset + (int len) - 1
            let ba = buf.[offset..last]
            offset <- offset + int len
            ba

    let read_byte (br: BinaryWasmStream) =
        br.ReadByte()

    let read_bytes (br: BinaryWasmStream) (len :uint32) =
        br.ReadBytes(len)

    let read_var_u32 (br: BinaryWasmStream) =
        br.ReadVarUInt32()

    let read_var_i32 (br: BinaryWasmStream) =
        br.ReadVarInt32()

    let read_var_i64 (br: BinaryWasmStream) =
        br.ReadVarInt64()

    let read_f32 (br: BinaryWasmStream) =
        let ba = read_bytes br 4u
        use ms = new System.IO.MemoryStream(ba)
        use br = new System.IO.BinaryReader(ms)
        br.ReadSingle()

    let read_f64 (br: BinaryWasmStream) =
        let ba = read_bytes br 8u
        use ms = new System.IO.MemoryStream(ba)
        use br = new System.IO.BinaryReader(ms)
        br.ReadDouble()

    let read_u32 br =
        let b0 = read_byte br |> uint32
        let b1 = read_byte br |> uint32
        let b2 = read_byte br |> uint32
        let b3 = read_byte br |> uint32

        let v = (b3 <<< 24) ||| (b2 <<< 16) ||| (b1 <<< 8) ||| (b0 <<< 0)
        v

    let remaining (br: BinaryWasmStream) =
        br.Remaining()

    let read_vector (br: BinaryWasmStream) count f =
        if count = 0u then 
            []
        else
            let rec g a =
                let it = f br
                let b = it :: a
                if (uint32 b.Length) = count then b else g b
            g [] |> List.rev
