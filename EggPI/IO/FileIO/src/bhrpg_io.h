#include <stdio.h>
#include <stdlib.h>

#define EXPORT __declspec(dllexport)
#define BHRPG_IO_CALLBACK __cdecl

typedef struct _BHRPGIO_Callbacks
{
    void* (BHRPG_IO_CALLBACK *Malloc)(size_t size);
    void  (BHRPG_IO_CALLBACK *Free)(void* memory);
    void  (BHRPG_IO_CALLBACK *NoMem)(void);
} BHRPGIO_Callbacks;

static BHRPGIO_Callbacks callbacks = { malloc, free, abort };

EXPORT extern int   SaveBytes(char* path, void* data, int len);
EXPORT extern void* LoadBytes(char* path, int* out_len);

EXPORT extern void* BHRPGIO_Malloc(size_t);
EXPORT extern void  BHRPGIO_Free(void*);

EXPORT extern void InitCallbacks(const BHRPGIO_Callbacks* callbacks);