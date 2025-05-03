using System.Text;

public class ARM64Generator
{
    private readonly List<string> _instructions = new List<string>();
    private readonly StandardLibrary _standardLibrary = new StandardLibrary();
    private readonly Dictionary<string, string> _stringConstants = new Dictionary<string, string>();
    private readonly Dictionary<string, int> _variableOffsets = new Dictionary<string, int>();
    private readonly Dictionary<string,string> _stringLiteralMap = new Dictionary<string,string>();
    private readonly List<string> _dataEntries = new List<string>();
    private int _stringConstantCounter = 0;
    private int _currentStackOffset = 0;
    public void EmitLabel(string label)
    {
        _instructions.Add($"{label}:");
    }

    public void EmitFunctionProlog()
    {
        // Reserva 128 bytes para todas las variables locales
        _instructions.Add("sub sp, sp, #128");
    }

    public void EmitFunctionEpilog()
    {
        // Restaurar el stack pointer si fue modificado
        // _instructions.Add("add sp, sp, #N");  // Donde N es el espacio reservado

        // Restaurar registros de enlace y frame pointer
        _instructions.Add("ldp x29, x30, [sp], #16");
    }

public void AllocateSlice(string id, string tipo, int count)
{
    Comment($"Creando slice '{id}' de tipo {tipo} y {count} elementos");

    int elementSize = 8;
    int dataSize = count * elementSize;
    int totalSize = dataSize + 8; // +8 bytes para almacenar el count

    // --- Reserva de Memoria ---
    Push(Register.X0); Push(Register.X1); Push(Register.X2); Push(Register.X3); // Guardar registros

    Mov(Register.X0, totalSize);
    Bl("malloc"); // X0 = dirección base del bloque completo (count + data)

    // --- Almacenar Count y Puntero a Datos ---
    Mov(Register.X1, count);
    Str(Register.X1, Register.X0, 0); // Guardar 'count' en los primeros 8 bytes [X0]

    Add(Register.X1, Register.X0, 8); // X1 = dirección del primer elemento de datos

    Mov(Register.X0, Register.X1); // Mover puntero a datos a X0 para StoreVariable
    StoreVariable(id);             // La variable 'id' ahora apunta al inicio de los datos

    // --- Almacenar Elementos ---
    // X1 todavía tiene el puntero a datos
    for (int i = count - 1; i >= 0; i--)
    {
        Pop(Register.X0); // Obtener valor del elemento de la pila
        Str(Register.X0, Register.X1, i * elementSize); // Guardar en datos[i]
        Comment($"Guardado elemento {i} en slice '{id}' en offset {i * elementSize}");
    }

    // --- Restaurar Registros ---
    Pop(Register.X3); Pop(Register.X2); Pop(Register.X1); Pop(Register.X0);
}

public void AllocateVariable(string varName)
{
    // Asignar un nuevo offset para la variable (16 bytes alineados)
    _currentStackOffset += 16;
    _variableOffsets[varName] = _currentStackOffset;
}

    public void StoreVariable(string name)
    {
        int offset = _variableOffsets[name];
        _instructions.Add($"// Guardando valor en variable {name} en offset {offset}");
        _instructions.Add($"str x0, [x29, #{offset}]");
    }

 public void LoadVariable(string register, string name)
{
    if (_variableOffsets.TryGetValue(name, out int offset))
    {
        _instructions.Add($"// Cargando {name} desde [x29+{offset}]");
        _instructions.Add($"ldr {register}, [x29, #{offset}]");
    }
    else
    {
        // tu fallback actual…
        _instructions.Add($"mov {register}, #0  // {name} no definida");
    }
}

    public bool HasVariable(string name)
    {
        return _variableOffsets.ContainsKey(name);
    }

    public void Ret()
    {
        _instructions.Add("ret");
    }

    public void Add(string rd, string rs1, string rs2)
    {
        _instructions.Add($"add {rd}, {rs1}, {rs2}");
    }
    public void Add(string rd, string rs1, int imm)
    {
        _instructions.Add($"add {rd}, {rs1}, #{imm}");
    }
    public void Sub(string rd, string rs1, string rs2)
    {
        _instructions.Add($"sub {rd}, {rs1}, {rs2}");
    }
    public void Sub(string rd, string rs1, int imm)
    {
        _instructions.Add($"sub {rd}, {rs1}, #{imm}");
    }
    public void Mul(string rd, string rs1, string rs2)
    {
        _instructions.Add($"mul {rd}, {rs1}, {rs2}");
    }
    public void Div(string rd, string rs1, string rs2)
    {
        _instructions.Add($"udiv {rd}, {rs1}, {rs2}");
    }
    public void Addi(string rd, string rs1, int imm)
    {
        _instructions.Add($"addi {rd}, {rs1}, #{imm}");
    }
    public void Str(string rs, string rt, int offset = 0)
    {
        _instructions.Add($"str {rs}, [{rt}, #{offset}]");
    }
    public void Ldr(string rd, string rt, int offset = 0)
    {
        _instructions.Add($"ldr {rd}, [{rt}, #{offset}]");
    }
     // para shiftear i << 3
    public void Lsl(string rd, string rn, int shift)
    {
        _instructions.Add($"lsl {rd}, {rn}, #{shift}");
    }

    // ldr con offset en registro
    public void Ldr(string rd, string rn,string rm)
    {
        _instructions.Add($"ldr {rd}, [{rn}, {rm}]");
    }

    // str con offset en registro (si lo necesitas en otros sitios)
    public void Str(string rs, string rn, string rm)
    {
        _instructions.Add($"str {rs}, [{rn}, {rm}]");
    }
    public void Mov(string rd, int imm)
    {
        _instructions.Add($"mov {rd}, #{imm}");
    }
    public void Mov(string rd, string rs)
    {
        _instructions.Add($"mov {rd}, {rs}");
    }

    public void Movz(string rd, ulong value, int shift = 0)
    {
        _instructions.Add($"movz {rd}, #{value & 0xFFFF}, lsl #{shift}");
    }
    
    public void Movk(string rd, ulong value, int shift = 0)
    {
        _instructions.Add($"movk {rd}, #{value & 0xFFFF}, lsl #{shift}");
    }
    
    public void Fmov(string fd, string xn)
    {
        _instructions.Add($"fmov {fd}, {xn}");
    }

    public void FAdd(string fd, string fs1, string fs2)
    {
        _instructions.Add($"fadd {fd}, {fs1}, {fs2}");
    }

    public void FMul(string fd, string fs1, string fs2)
    {
        _instructions.Add($"fmul {fd}, {fs1}, {fs2}");
    }
    public void PushFloat()
    {
        _instructions.Add($"str d0, [sp, #-16]!");
    }
    
    public void PopFloat()
    {
        _instructions.Add($"ldr d0, [sp], #16");
    }

    public void Csel(string rd, string rn, string rm, string condition)
    {
        _instructions.Add($"csel {rd}, {rn}, {rm}, {condition}");
    }
    public void B(string cond, string label)
    {
        _instructions.Add($"b{cond} {label}");
    }
    
public void StoreVariableFloat(string varName)
{
    if (_variableOffsets.TryGetValue(varName, out int offset))
    {
        // offset medido desde el frame pointer x29, no desde sp
        _instructions.Add($"str d0, [x29, #{offset}]");
    }
    else
    {
        throw new Exception($"Variable {varName} no definida");
    }
}

public void LoadVariableFloat(string varName)
{
    if (_variableOffsets.TryGetValue(varName, out int offset))
    {
        // ldr d0, [x29, #offset]
        _instructions.Add($"ldr d0, [x29, #{offset}]");
    }
    else
    {
        throw new Exception($"Variable {varName} no definida");
    }
}


    public void IntToFloat()
    {
        _instructions.Add($"scvtf d0, x0");
    }

public string RegisterFloat(double value)
{
    string label = $"float_{_stringConstantCounter++}";
    _instructions.Add($".data");
    _instructions.Add($"    .align 8");
    _instructions.Add($"{label}:");
    _instructions.Add($"    .double {value}");
    _instructions.Add($".text");
    return label;
}

public string RegisterIntSlice(string name, int[] valores)
    {
        if (!_dataEntries.Any(e => e.StartsWith(name + ":")))
        {
            _dataEntries.Add($"{name}:");
            _dataEntries.Add($"    .word {string.Join(", ", valores)}");
        }
        return name;
    }


public void LoadFloatValue(double value)
{
    string label = RegisterFloat(value);
    _instructions.Add($"adr x0, {label}");
    _instructions.Add($"ldr d0, [x0]");
}

    public void FSub(string fd, string fs1, string fs2)
    {
        _instructions.Add($"fsub {fd}, {fs1}, {fs2}");
    }

    public void FDiv(string fd, string fs1, string fs2)
    {
        _instructions.Add($"fdiv {fd}, {fs1}, {fs2}");
    }

    public void Adr(string rd, string label)
    {
        _instructions.Add($"adr {rd}, {label}");
    }
    public string RegisterString(string value)
    {
        // si ya existía esa cadena, retorno el mismo label
        if (_stringLiteralMap.TryGetValue(value, out var lbl))
            return lbl;

        // si no, genero uno nuevo
        string label = $"str_const_{_stringConstants.Count}";
        _stringConstants[label] = value;
        _stringLiteralMap[value] = label;
        return label;
    }

    public void Push(string reg)
    {
        _instructions.Add($"str {reg}, [sp, #-16]!");
    }
    public void Pop(string reg)
    {
        _instructions.Add($"ldr {reg}, [sp], #16");
    }
    public void Bl(string label)
    {
        _instructions.Add($"bl {label}");
        _standardLibrary.Use(label);
    }
    public void Beq(string label)
    {
        _instructions.Add($"beq {label}");
    }
    public void B(string label)
    {
        _instructions.Add($"b {label}");
    }
    public void Cset(string rd, string condition)
    {
        _instructions.Add($"cset {rd}, {condition}");
    }
    public void Fcmp(string fs1, string fs2)
    {
        _instructions.Add($"fcmp {fs1}, {fs2}");
    }
    public void AdrIfNotZero(string register, string label)
{
    _instructions.Add($"csel {register}, {register}, {label}, ne");
}

    public void Cmp(string register, int value)
    {
        _instructions.Add($"cmp {register}, #{value}");
    }
    public void Cmp(string register1, string register2)
    {
        _instructions.Add($"cmp {register1}, {register2}");
    }
    public void Orr(string rd, string rs1, string rs2)
    {
        _instructions.Add($"orr {rd}, {rs1}, {rs2}");
    }
    public void And(string rd, string rs1, string rs2)
    {
        _instructions.Add($"and {rd}, {rs1}, {rs2}");
    }
    public void Neg(string rd, string rs)
    {
        _instructions.Add($"neg {rd}, {rs}");
    }
    public void Eor(string rd, string rs1, int rs2)
    {
        _instructions.Add($"eor {rd}, {rs1}, #{rs2}");
    }
    public void Bne(string label)
    {
        _instructions.Add($"bne {label}");
    }
    public void Bge(string label)
    {
        _instructions.Add($"bge {label}");
    }
    public void Svc()
    {
        _instructions.Add("svc #0");
    }
    public void EndProgram()
    {
        // Devuelve SP y registros, luego llamada a exit
        _instructions.Add("add sp, sp, #128");
        _instructions.Add("ldp x29, x30, [sp], #16");
        _instructions.Add("mov x0, #0");
        _instructions.Add("mov x8, #93");
        _instructions.Add("svc #0");
    }
    public void PrintInteger(string rs)
    {
        _standardLibrary.Use("print_integer");
        _instructions.Add($"mov x0, {rs}"); // Número a imprimir
        _instructions.Add($"bl print_integer"); // Llamar a la función
    }
    public void PrintString(string value)
    {
        _standardLibrary.Use("print_string");
        string label = RegisterString(value);
        _instructions.Add($"adr x0, {label}");
        _instructions.Add($"bl print_string");
    }
    public void PrintFloat()
    {
        _standardLibrary.Use("print_float");
        _instructions.Add($"bl print_float"); // Llamar a la función
    }
    
    public void PrintNewLine()
    {
        _standardLibrary.Use("print_newline");
        _instructions.Add($"bl print_newline"); // Llamar a la función
    }
    public void Comment(string comment)
    {
        _instructions.Add($"// {comment}");
    }
    
public override string ToString()
{
    var sb = new StringBuilder();
    sb.AppendLine("// Generando código ARM64-201708880");
    
    // Sección .data
    sb.AppendLine(".data");
    
    // Definir constantes de string
    foreach (var kv in _stringConstants)
    {
        var value = kv.Value.Replace("\n", "\\n");
        sb.AppendLine($"{kv.Key}:");
        sb.AppendLine($"    .ascii \"{value}\"");
        sb.AppendLine("    .align 4");
    }
    // luego los slices
    foreach (var entry in _dataEntries)
    {
        sb.AppendLine(entry);
    }
    
    // Sección .text
    sb.AppendLine("\n.text");
    sb.AppendLine(".global _start");         
    sb.AppendLine(".extern printf");       // Declarar printf como externo
    
    sb.AppendLine("_start:");                
    
    // Configuración del frame
    sb.AppendLine("    stp x29, x30, [sp, #-16]!");
    sb.AppendLine("    mov x29, sp");

    // Instrucciones generadas
    foreach (var instruction in _instructions)
    {
        if (instruction.EndsWith(":"))
            sb.AppendLine(instruction);
        else
            sb.AppendLine("    " + instruction);
    }

    // Epilogo y retorno
    sb.AppendLine("    mov w0, #0");      // Valor de retorno 0
    sb.AppendLine("    ldp x29, x30, [sp], #16");
    sb.AppendLine("    ret");             // Usar ret en lugar de svc

    // Incluir las funciones de la biblioteca estándar que fueron usadas
    sb.AppendLine("\n// Standard library functions");
    sb.AppendLine(_standardLibrary.GetFunctionDefinitions());

    return sb.ToString();
}
}