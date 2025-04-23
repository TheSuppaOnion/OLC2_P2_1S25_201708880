using System.Text;

public class ARM64Generator
{
    private readonly List<string> _instructions = new List<string>();
    private readonly StandardLibrary _standardLibrary = new StandardLibrary();
    private readonly Dictionary<string, string> _stringConstants = new Dictionary<string, string>();
    private int _stringConstantCounter = 0;
    public string RegisterStringConstant(string value)
    {
        if (!_stringConstants.ContainsKey(value))
        {
            string label = $"string_constant_{_stringConstantCounter++}";
            _stringConstants[value] = label;
        }
        return _stringConstants[value];
    }
    public void EmitLabel(string label)
    {
        _instructions.Add($"{label}:");
    }

    public void EmitFunctionProlog()
    {
        // Guardar registros de enlace y frame pointer
        _instructions.Add("stp x29, x30, [sp, #-16]!");
        _instructions.Add("mov x29, sp");

        // Reservar espacio para variables locales si es necesario
        // _instructions.Add("sub sp, sp, #N");  // Donde N es el espacio necesario
    }

    public void EmitFunctionEpilog()
    {
        // Restaurar el stack pointer si fue modificado
        // _instructions.Add("add sp, sp, #N");  // Donde N es el espacio reservado

        // Restaurar registros de enlace y frame pointer
        _instructions.Add("ldp x29, x30, [sp], #16");
    }

    public void Ret()
    {
        _instructions.Add("ret");
    }

    public void Add(string rd, string rs1, string rs2)
    {
        _instructions.Add($"add {rd}, {rs1}, {rs2}");
    }
    public void Sub(string rd, string rs1, string rs2)
    {
        _instructions.Add($"sub {rd}, {rs1}, {rs2}");
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
    public void Mov(string rd, int imm)
    {
        _instructions.Add($"mov {rd}, #{imm}");
    }
    public void Mov(string rd, string rs)
    {
        _instructions.Add($"mov {rd}, {rs}");
    }
    public void LoadVariable(string register, string varName)
{
    _instructions.Add($"// Cargar variable {varName}");
    _instructions.Add($"ldr {register}, [x29, #var_{varName}_offset]");
}

    public void LoadStringVariable(string register, string varName)
    {
        _instructions.Add($"// Cargar dirección de cadena {varName}");
        _instructions.Add($"adr {register}, str_{varName}");
    }
    public void Adr(string rd, string label)
    {
        _instructions.Add($"adr {rd}, {label}");
    }
    public string RegisterString(string value)
    {
        string label = $"str_const_{_stringConstants.Count}";
        _stringConstants[label] = value;
        _stringConstantCounter++;
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
    public void Svc()
    {
        _instructions.Add("svc #0");
    }
    public void EndProgram()
    {
        _instructions.Add("ldp x29, x30, [sp], #16");
        Mov(Register.X0, 0);
        Mov(Register.X8, 93); // syscall para salir
        Svc();
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
        string label = RegisterStringConstant(value);
        _instructions.Add($"adr x0, {label}");
        _instructions.Add($"bl print_string");
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

        // Sección .data primero
        sb.AppendLine(".data");

        // Definir datos constantes necesarios
        foreach (var stringConst in _stringConstants)
        {
            sb.AppendLine($"{stringConst.Key}: .asciz \"{stringConst.Value}\"");
            sb.AppendLine("    .align 4");  // Alineación importante
        }

        // Definir otras constantes necesarias
        sb.AppendLine("newline_char:");
        sb.AppendLine("    .byte   10");    // ASCII code for newline character
        sb.AppendLine("    .align  4");     // Alineación importante

        // Luego la sección .text
        sb.AppendLine();
        sb.AppendLine(".text");
        sb.AppendLine(".global _start");
        sb.AppendLine("_start:");

        // Configuración del frame pointer
        sb.AppendLine("    // Configuración del frame");
        sb.AppendLine("    stp x29, x30, [sp, #-16]!");
        sb.AppendLine("    mov x29, sp");

        // Instrucciones generadas
        foreach (var instruction in _instructions)
        {
            // Si es una etiqueta, no aplicar indentación
            if (instruction.EndsWith(":"))
                sb.AppendLine(instruction);
            else
                sb.AppendLine("    " + instruction);
        }

        // Incluir las funciones de la biblioteca estándar
        sb.AppendLine();
        sb.AppendLine(_standardLibrary.GetFunctionDefinitions());

        return sb.ToString();
    }
}