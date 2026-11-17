// RonJames Foundation requires Odin Inspector (ADR 0001). If Odin is missing, every package assembly
// fails with "The type or namespace name 'Sirenix' could not be found"; this warning names the cause.
//
// It is a warning, not an error: on the very first import of Odin and this package together, the
// package can compile once before Odin has added its ODIN_INSPECTOR define, and an #error there
// would be a false alarm. Odin then adds the define and the warning disappears.
#if !ODIN_INSPECTOR
#warning RonJames Foundation requires Odin Inspector 3.3 or newer in this project (Assets/Plugins/Sirenix). If you see "Sirenix could not be found" errors, import Odin.
#endif
