#include "bhrpg_io.h"
#include <sys/stat.h>

EXPORT void
InitCallbacks(const BHRPGIO_Callbacks* cbacks)
{
    if(cbacks->Malloc != NULL)
    {
        callbacks.Malloc = cbacks->Malloc;
    }

    if(cbacks->Free != NULL)
    {
        callbacks.Free = cbacks->Free;
    }

    if(cbacks->NoMem != NULL)
    {
        callbacks.NoMem = cbacks->NoMem;
    }
}

EXPORT int 
SaveBytes(char* path, void* data, int len)
{
    FILE* fp;

    fp = fopen(path, "wb");

    if(fp == NULL) { return -1; }

    int num_written = fwrite(data, 1, len, fp);

    fclose(fp);

    return num_written;
}

EXPORT void*
LoadBytes(char* path, int* out_len)
{
    FILE* fp;
    fp = fopen(path, "rb");

    // Get file size (in bytes).
    fseek(fp, 0, SEEK_END);
    *out_len = ftell(fp);
    rewind(fp);    

    // Allocate buffer large enough to store all of the file's bytes.
    void* buf = BHRPGIO_Malloc(*out_len);

    fread(buf, 1, *out_len, fp);

    fclose(fp);

    return buf;
}

EXPORT void*
BHRPGIO_Malloc(size_t size)
{
    void* buf = callbacks.Malloc(size);

    if(buf == NULL)
    {
        callbacks.NoMem();
    }

    return buf;
}

EXPORT void
BHRPGIO_Free(void* buf)
{
    callbacks.Free(buf);
}