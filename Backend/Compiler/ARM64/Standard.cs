using System.Collections.Generic;

public class StandardLibrary
{
    private readonly HashSet<string> UsedFunctions = new HashSet<string>();

    public void Use(string function)
    {
        UsedFunctions.Add(function);
    }

    public string GetFunctionDefinitions()
    {
        var functions = new List<string>();

        foreach (var function in UsedFunctions)
        {
            if (FunctionDefinitions.TryGetValue(function, out var definition))
            {
                functions.Add(definition);
            }
        }


        return string.Join("\n\n", functions);
    }

    private readonly static Dictionary<string, string> FunctionDefinitions = new Dictionary<string, string>
    {
        { "print_integer", @"
//--------------------------------------------------------------
// print_integer - Prints a signed integer to stdout
//
// Input:
//   x0 - The integer value to print
//--------------------------------------------------------------
print_integer:
    // Save registers
    stp x29, x30, [sp, #-16]!  // Save frame pointer and link register
    stp x19, x20, [sp, #-16]!  // Save callee-saved registers
    stp x21, x22, [sp, #-16]!
    stp x23, x24, [sp, #-16]!
    stp x25, x26, [sp, #-16]!
    stp x27, x28, [sp, #-16]!
    
    // Check if number is negative
    mov x19, x0                // Save original number
    cmp x19, #0                // Compare with zero
    bge positive_number        // Branch if greater or equal to zero
    
    // Handle negative number
    mov x0, #1                 // fd = 1 (stdout)
    adr x1, minus_sign         // Address of minus sign
    mov x2, #1                 // Length = 1
    mov w8, #64                // Syscall write
    svc #0
    
    neg x19, x19               // Make number positive
    
positive_number:
    // Prepare buffer for converting result to ASCII
    sub sp, sp, #32            // Reserve space on stack
    mov x22, sp                // x22 points to buffer
    
    // Initialize digit counter
    mov x23, #0                // Digit counter
    
    // Handle special case for zero
    cmp x19, #0
    bne convert_loop
    
    // If number is zero, just write '0'
    mov w24, #48               // ASCII '0'
    strb w24, [x22, x23]       // Store in buffer
    add x23, x23, #1           // Increment counter
    b print_result             // Skip conversion loop
    
convert_loop:
    // Divide the number by 10
    mov x24, #10
    udiv x25, x19, x24         // x25 = x19 / 10 (quotient)
    msub x26, x25, x24, x19    // x26 = x19 - (x25 * 10) (remainder)
    
    // Convert remainder to ASCII and store in buffer
    add x26, x26, #48          // Convert to ASCII ('0' = 48)
    strb w26, [x22, x23]       // Store digit in buffer
    add x23, x23, #1           // Increment digit counter
    
    // Prepare for next iteration
    mov x19, x25               // Quotient becomes the new number
    cbnz x19, convert_loop     // If number is not zero, continue
    
    // Reverse the buffer since digits are in reverse order
    mov x27, #0                // Start index
reverse_loop:
    sub x28, x23, x27          // x28 = length - current index
    sub x28, x28, #1           // x28 = length - current index - 1
    
    cmp x27, x28               // Compare indices
    bge print_result           // If crossed, finish reversing
    
    // Swap characters
    ldrb w24, [x22, x27]       // Load character from start
    ldrb w25, [x22, x28]       // Load character from end
    strb w25, [x22, x27]       // Store end character at start
    strb w24, [x22, x28]       // Store start character at end
    
    add x27, x27, #1           // Increment start index
    b reverse_loop             // Continue reversing
    
print_result:
    // Add newline
    mov w24, #10               // Newline character
    strb w24, [x22, x23]       // Add to end of buffer
    add x23, x23, #1           // Increment counter
    
    // Print the result
    mov x0, #1                 // fd = 1 (stdout)
    mov x1, x22                // Buffer address
    mov x2, x23                // Buffer length
    mov w8, #64                // Syscall write
    svc #0
    
    // Clean up and restore registers
    add sp, sp, #32            // Free buffer space
    ldp x27, x28, [sp], #16    // Restore callee-saved registers
    ldp x25, x26, [sp], #16
    ldp x23, x24, [sp], #16
    ldp x21, x22, [sp], #16
    ldp x19, x20, [sp], #16
    ldp x29, x30, [sp], #16    // Restore frame pointer and link register
    ret                        // Return to caller

minus_sign:
    .ascii ""-""               // Minus sign"
    },

    { "print_string", @"
//--------------------------------------------------------------
// print_string - Prints a null-terminated string to stdout
//
// Input:
//   x0 - The address of the null-terminated string to print
//--------------------------------------------------------------
print_string:
    // Save link register and other registers we'll use
    stp     x29, x30, [sp, #-16]!
    stp     x19, x20, [sp, #-16]!
    stp     x21, x22, [sp, #-16]!
    
    // x19 will hold the string address
    mov     x19, x0
    
    // Calculate string length first
    mov     x21, #0              // Length counter
string_length:
    ldrb    w20, [x19, x21]     // Load byte from string
    cbz     w20, print_string_now // If zero, end of string
    add     x21, x21, #1        // Increment counter
    b       string_length       // Continue loop
    
print_string_now:
    // Now we have the length in x21
    // If length is zero, skip printing
    cbz     x21, print_done
    
    // Print the entire string with one syscall
    mov     x0, #1              // File descriptor: 1 for stdout
    mov     x1, x19             // Address of the string
    mov     x2, x21             // Length of the string
    mov     x8, #64             // syscall: write (64 on ARM64)
    svc     #0                  // Make the syscall
    
print_done:
    // Restore saved registers
    ldp     x21, x22, [sp], #16
    ldp     x19, x20, [sp], #16
    ldp     x29, x30, [sp], #16
    ret
"},

    { "print_newline", @"
//--------------------------------------------------------------
// print_newline - Prints a newline character to stdout
//
// Input: None
//--------------------------------------------------------------
print_newline:
    // Save link register
    stp     x29, x30, [sp, #-16]!
    
    // Write the newline character to stdout
    mov     x0, #1              // File descriptor: 1 for stdout
    adr     x1, newline_char    // Address of the newline character
    mov     x2, #1              // Length: 1 byte
    mov     x8, #64             // syscall: write (64 on ARM64)
    svc     #0                  // Make the syscall
    
    // Restore link register and return
    ldp     x29, x30, [sp], #16
    ret
    " },
    { "concatenate_strings", @"
//--------------------------------------------------------------
// concatenate_strings - Concatenates two null-terminated strings
//
// Input:
//   x0 - The address of the first null-terminated string
//   x1 - The address of the second null-terminated string
// Output:
//   x0 - The address of the resulting concatenated string
//--------------------------------------------------------------
concatenate_strings:
    // Save registers
    stp     x29, x30, [sp, #-16]!
    stp     x19, x20, [sp, #-16]!
    stp     x21, x22, [sp, #-16]!
    stp     x23, x24, [sp, #-16]!
    
    // Save input strings
    mov     x19, x0              // First string
    mov     x20, x1              // Second string
    
    // Calculate length of first string
    mov     x21, #0              // Length counter
first_string_len:
    ldrb    w22, [x19, x21]      // Load byte from first string
    cbz     w22, first_string_done // If zero, end of string
    add     x21, x21, #1         // Increment counter
    b       first_string_len     // Continue loop
first_string_done:
    
    // Calculate length of second string
    mov     x22, #0              // Length counter
second_string_len:
    ldrb    w23, [x20, x22]      // Load byte from second string
    cbz     w23, second_string_done // If zero, end of string
    add     x22, x22, #1         // Increment counter
    b       second_string_len    // Continue loop
second_string_done:
    
    // Calculate total length needed (sum + 1 for null terminator)
    add     x23, x21, x22
    add     x23, x23, #1
    
    // Allocate memory for new string (using stack for simplicity)
    // For a real implementation, you might use heap allocation
    sub     sp, sp, x23          // Reserve space on stack
    mov     x24, sp              // x24 points to new string buffer
    
    // Copy first string
    mov     x0, #0               // Offset counter
copy_first:
    cmp     x0, x21              // Check if we've reached end of first string
    beq     copy_second_prep     // If yes, prepare to copy second string
    ldrb    w1, [x19, x0]        // Load byte from first string
    strb    w1, [x24, x0]        // Store byte in new buffer
    add     x0, x0, #1           // Increment counter
    b       copy_first           // Continue loop
copy_second_prep:
    
    // Copy second string
    mov     x1, #0               // Offset counter for second string
copy_second:
    cmp     x1, x22              // Check if we've reached end of second string
    beq     concat_done          // If yes, we're done
    ldrb    w2, [x20, x1]        // Load byte from second string
    add     x3, x0, x1           // Calculate position in new buffer
    strb    w2, [x24, x3]        // Store byte in new buffer
    add     x1, x1, #1           // Increment counter
    b       copy_second          // Continue loop
concat_done:
    
    // Add null terminator
    add     x0, x0, x22          // Position after both strings
    mov     w1, #0               // Null terminator
    strb    w1, [x24, x0]        // Store null terminator
    
    // Return pointer to new string
    mov     x0, x24
    
    // Registers are restored at the end of the function
    // Note: The allocated memory on stack remains valid 
    // until this function's caller returns
    
    // Restore registers
    ldp     x23, x24, [sp, x23]  // Restore registers (offsetting by allocated size)
    ldp     x21, x22, [sp, #16]
    ldp     x19, x20, [sp, #32]
    ldp     x29, x30, [sp, #48]
    add     sp, sp, #64          // Restore stack pointer
    ret
"},

{ "convert_int_to_string", @"
//--------------------------------------------------------------
// convert_int_to_string - Converts an integer to a string
//
// Input:
//   x0 - The integer value to convert
// Output:
//   x0 - The address of the resulting string
//--------------------------------------------------------------
convert_int_to_string:
    // Save registers
    stp     x29, x30, [sp, #-16]!
    stp     x19, x20, [sp, #-16]!
    stp     x21, x22, [sp, #-16]!
    stp     x23, x24, [sp, #-16]!
    
    // x19 will hold our integer
    mov     x19, x0
    
    // Allocate space for string on stack (max 21 bytes for 64-bit int plus sign and null terminator)
    sub     sp, sp, #32
    mov     x22, sp              // x22 points to buffer
    
    // Check if number is negative
    cmp     x19, #0
    bge     convert_positive
    
    // Handle negative
    mov     w20, #45             // ASCII for '-'
    strb    w20, [x22]           // Store minus sign
    mov     x21, #1              // Start position after minus
    neg     x19, x19             // Make number positive
    b       begin_conversion
    
convert_positive:
    mov     x21, #0              // Start at beginning of buffer
    
begin_conversion:
    // Handle special case for zero
    cmp     x19, #0
    bne     conversion_loop
    
    // If number is zero, just use '0'
    mov     w20, #48             // ASCII for '0'
    strb    w20, [x22, x21]      // Store in buffer
    add     x21, x21, #1         // Increment position
    b       conversion_end
    
conversion_loop:
    // Continue until number becomes 0
    cmp     x19, #0
    beq     reverse_string
    
    // Get last digit (remainder when divided by 10)
    mov     x23, #10
    udiv    x24, x19, x23        // x24 = x19 / 10
    msub    x20, x24, x23, x19   // x20 = x19 - (x24 * 10) = remainder
    
    // Convert digit to ASCII and store
    add     w20, w20, #48        // Convert to ASCII
    strb    w20, [x22, x21]      // Store in buffer
    add     x21, x21, #1         // Increment position
    
    // Divide number by 10 for next iteration
    mov     x19, x24
    b       conversion_loop
    
reverse_string:
    // x21 now contains the length of the string (excluding null terminator)
    
    // For a proper string, we need to reverse it (except minus sign if present)
    mov     x23, #0              // Start index
    cmp     x23, #0
    bne     start_reversal       // If we have a minus sign, start after it
    
start_reversal:
    sub     x24, x21, #1         // End index (last character)
    
reverse_loop:
    cmp     x23, x24             // Check if indices have crossed
    bge     conversion_end
    
    // Swap characters
    ldrb    w19, [x22, x23]      // Load character from start
    ldrb    w20, [x22, x24]      // Load character from end
    strb    w20, [x22, x23]      // Store end character at start
    strb    w19, [x22, x24]      // Store start character at end
    
    add     x23, x23, #1         // Increment start index
    sub     x24, x24, #1         // Decrement end index
    b       reverse_loop
    
conversion_end:
    // Add null terminator
    mov     w20, #0              // ASCII NUL
    strb    w20, [x22, x21]      // Store at end of buffer
    
    // Return buffer pointer
    mov     x0, x22
    
    // Restore registers (we don't restore stack pointer to keep our string buffer)
    ldp     x23, x24, [sp, #32]
    ldp     x21, x22, [sp, #16]
    ldp     x19, x20, [sp, #0]
    // Note: We're keeping the allocated stack space since it contains our string
    // This means the caller must be careful about stack usage
    
    ret
"},

{ "convert_float64_to_string", @"
//--------------------------------------------------------------
// convert_float64_to_string - Converts a float to a string
//
// Input:
//   d0 - The float value to convert
// Output:
//   x0 - The address of the resulting string
//--------------------------------------------------------------
convert_float64_to_string:
    // This is a simplified implementation that converts to fixed-point
    // A full implementation would be much more complex
    
    // Save registers
    stp     x29, x30, [sp, #-16]!
    stp     x19, x20, [sp, #-16]!
    
    // Allocate buffer on stack (32 bytes should be enough for most floats)
    sub     sp, sp, #32
    mov     x19, sp
    
    // Convert to integer part
    fcvtzs  x0, d0              // Convert to signed integer
    
    // Call integer to string conversion
    bl      convert_int_to_string
    
    // Append decimal point
    mov     w1, #46             // ASCII for '.'
    strb    w1, [x0, #1]        // Append after first digit
    
    // Return pointer to string
    mov     x0, x19
    
    // Restore registers (again, not restoring stack to keep buffer)
    ldp     x19, x20, [sp, #0]
    // We don't restore sp since we're returning our stack buffer
    
    ret
"},

{ "convert_bool_to_string", @"
//--------------------------------------------------------------
// convert_bool_to_string - Converts a boolean to ""true"" or ""false""
//
// Input:
//   x0 - The boolean value (0 = false, non-zero = true)
// Output:
//   x0 - The address of the resulting string
//--------------------------------------------------------------
convert_bool_to_string:
    cmp     x0, #0
    beq     return_false
    
    // Return ""true""
    adr     x0, str_true
    ret
    
return_false:
    // Return ""false""
    adr     x0, str_false
    ret
    
str_true:
    .asciz  ""true""
str_false:
    .asciz  ""false""
"}
    };
}