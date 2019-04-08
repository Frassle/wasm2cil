module Tests

open System
open Xunit

open wasm.def_basic
open wasm.def_instr
open wasm.def
open wasm.read
open wasm.write
open wasm.cecil
open wasm.builder

let newid () =
    Guid
        .NewGuid()
        .ToString()
        .Replace("{", "")
        .Replace("}", "")
        .Replace("-", "")

type AssemblyStuff = {
    a : System.Reflection.Assembly
    ns : string
    classname : string
    }

let prep_assembly m =
    let id = newid ()
    let ns = "test_namespace"
    let classname = "foo"
    let ver = new System.Version(1, 0, 0, 0)
    let ba = 
        use ms = new System.IO.MemoryStream()
        gen_assembly m id ns classname ver ms
        ms.ToArray()
    let a = System.Reflection.Assembly.Load(ba)
    Assert.NotNull(a)
    { a = a; classname = classname; ns = ns; }

let get_method a name =
    let fullname = sprintf "%s.%s" a.ns a.classname
    let t = a.a.GetType(fullname)
    let mi = t.GetMethod(name)
    Assert.NotNull(mi)
    mi

let check_0 (mi : System.Reflection.MethodInfo) (f : Unit -> 'b) =
    let r = mi.Invoke(null, null)
    let x = unbox<'b> r
    let should_be = f ()
    let eq = should_be = x
    Assert.True(eq)

let check_1 (mi : System.Reflection.MethodInfo) (f : 'a -> 'b) (n : 'a) =
    let args = [| box n |]
    let r = mi.Invoke(null, args)
    let x = unbox<'b> r
    let should_be = f n
    let eq = should_be = x
    Assert.True(eq)

let check_2 (mi : System.Reflection.MethodInfo) (f : 'a -> 'a -> 'b) (x : 'a) (y : 'a) =
    let args = [| box x; box y |]
    let r = mi.Invoke(null, args)
    let result = unbox<'b> r
    let should_be = f x y
    let eq = should_be = result
    Assert.True(eq)

[<Fact>]
let empty_module () =
    let m = {
        version = 1u
        sections = Array.empty
        }

    let a = prep_assembly m
    Assert.NotNull(a.a)

[<Fact>]
let empty_method () =
    let fb = FunctionBuilder()
    let name = "empty"
    fb.Name <- Some name
    fb.ReturnType <- None
    fb.Add (End)

    let b = ModuleBuilder()
    b.AddFunction(fb)

    let m = b.CreateModule()

    let a = prep_assembly m
    let mi = get_method a name
    mi.Invoke(null, null) |> ignore
    Assert.True(true)

[<Fact>]
let int_constant () =
    let fb = FunctionBuilder()
    let num = 42
    let name = "constant"
    fb.Name <- Some name
    fb.ReturnType <- Some I32
    fb.Add (I32Const num)
    fb.Add (End)

    let b = ModuleBuilder()
    b.AddFunction(fb)

    let m = b.CreateModule()

    let a = prep_assembly m
    let mi = get_method a name

    let impl n =
        num

    check_0 mi impl

[<Fact>]
let drop_empty () =
    let fb = FunctionBuilder()
    let name = "drop_empty"
    fb.Name <- Some name
    fb.ReturnType <- None
    fb.Add (Drop)
    fb.Add (End)

    let b = ModuleBuilder()
    b.AddFunction(fb)

    let m = b.CreateModule()

    Assert.Throws<InvalidOperationException>(fun () -> prep_assembly m |> ignore)

[<Fact>]
let simple_add () =
    let fb = FunctionBuilder()
    let addnum = 42
    let name = sprintf "add_%d" addnum
    fb.Name <- Some name
    fb.ReturnType <- Some I32
    fb.AddParam I32
    fb.Add (LocalGet (LocalIdx 0u))
    fb.Add (I32Const addnum)
    fb.Add I32Add
    fb.Add (End)

    let b = ModuleBuilder()
    b.AddFunction(fb)

    let m = b.CreateModule()

    let a = prep_assembly m
    let mi = get_method a name

    let impl n =
        n + addnum

    let check =
        check_1 mi impl

    check 13
    check 22

[<Fact>]
let simple_two_funcs () =

    let fb_add = FunctionBuilder()
    let addnum = 42
    let name = sprintf "add_%d" addnum
    fb_add.Name <- Some name
    fb_add.ReturnType <- Some I32
    fb_add.AddParam I32
    fb_add.Add (LocalGet (LocalIdx 0u))
    fb_add.Add (I32Const addnum)
    fb_add.Add I32Add
    fb_add.Add (End)

    let fb_mul = FunctionBuilder()
    let mulnum = 3
    let name = sprintf "mul_%d" mulnum
    fb_mul.Name <- Some name
    fb_mul.ReturnType <- Some I32
    fb_mul.AddParam I32
    fb_mul.Add (LocalGet (LocalIdx 0u))
    fb_mul.Add (I32Const mulnum)
    fb_mul.Add I32Mul
    fb_mul.Add (End)

    let fb_calc = FunctionBuilder()
    let subnum = 7
    let name = "calc"
    fb_calc.Name <- Some name
    fb_calc.ReturnType <- Some I32
    fb_calc.AddParam I32
    fb_calc.Add (LocalGet (LocalIdx 0u))
    fb_calc.Add (Call (FuncIdx 0u))
    fb_calc.Add (Call (FuncIdx 1u))
    fb_calc.Add (I32Const subnum)
    fb_calc.Add I32Sub
    fb_calc.Add (End)

    let b = ModuleBuilder()
    b.AddFunction(fb_add)
    b.AddFunction(fb_mul)
    b.AddFunction(fb_calc)

    let m = b.CreateModule()

    let a = prep_assembly m
    let mi = get_method a "calc"

    let impl n =
        ((n + addnum) * mulnum) - subnum

    let check =
        check_1 mi impl

    check -1
    check 0
    check 1
    check 13
    check 22

let make_simple_compare_func name t op =
    let fb = FunctionBuilder()
    fb.Name <- Some name
    fb.ReturnType <- Some I32
    fb.AddParam t
    fb.AddParam t
    fb.Add (LocalGet (LocalIdx 0u))
    fb.Add (LocalGet (LocalIdx 1u))
    fb.Add op
    fb.Add (End)

    let b = ModuleBuilder()
    b.AddFunction(fb)

    let m = b.CreateModule()
    m

let make_simple_compare_check t wasm_op fs_op =
    let name = "compare"
    let m = make_simple_compare_func name t wasm_op
    let a = prep_assembly m
    let mi = get_method a name

    let impl a b =
        if fs_op a b then 1 else 0

    let check =
        check_2 mi impl

    check

[<Fact>]
let i32_gt () =
    let check = make_simple_compare_check I32 I32GtS (>)
    for x = -4 to 4 do
        for y = -4 to 4 do
            check x y

[<Fact>]
let i32_lt () =
    let check = make_simple_compare_check I32 I32LtS (<)
    for x = -4 to 4 do
        for y = -4 to 4 do
            check x y

[<Fact>]
let i32_ge () =
    let check = make_simple_compare_check I32 I32GeS (>=)
    for x = -4 to 4 do
        for y = -4 to 4 do
            check x y

[<Fact>]
let i32_le () =
    let check = make_simple_compare_check I32 I32LeS (<=)
    for x = -4 to 4 do
        for y = -4 to 4 do
            check x y

[<Fact>]
let simple_loop_optimized_out () =
    (*

int foo(int x)
{
    int r = 0;
    for (int i=0; i<x; i++)
    {
        r += i;
    }
    return r;
}

    *)

    let fb = FunctionBuilder()
    let name = "foo"
    fb.Name <- Some name
    fb.ReturnType <- Some I32
    fb.AddParam I32

    fb.Add (Block (Some I32))
    fb.Add (LocalGet (LocalIdx 0u))
    fb.Add (I32Const 1)
    fb.Add (I32LtS)
    fb.Add (BrIf (LabelIdx 0u))
    fb.Add (LocalGet (LocalIdx 0u))
    fb.Add (I32Const -1)
    fb.Add (I32Add)
    fb.Add (I64ExtendI32U)
    fb.Add (LocalGet (LocalIdx 0u))
    fb.Add (I32Const -2)
    fb.Add (I32Add)
    fb.Add (I64ExtendI32U)
    fb.Add (I64Mul)
    fb.Add (I64Const 1L)
    fb.Add (I64ShrU)
    fb.Add (I32WrapI64)
    fb.Add (LocalGet (LocalIdx 0u))
    fb.Add (I32Add)
    fb.Add (I32Const -1)
    fb.Add (I32Add)
    fb.Add (Return)
    fb.Add (End)
    fb.Add (I32Const 0)
    fb.Add (End)

    let b = ModuleBuilder()
    b.AddFunction(fb)

    let m = b.CreateModule()

    let a = prep_assembly m
    let mi = get_method a name

    let impl x =
        let mutable r = 0
        for i = 0 to (x - 1) do
            r <- r + i
        r

    let check =
        check_1 mi impl

    check 0
    check 1
    check 13
    check -5
    check 22

