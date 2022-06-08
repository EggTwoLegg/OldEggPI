using System;
using System.Net;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

using EggPI.Nav;


namespace EggPI
{
	public unsafe partial struct NativeDataReader
	{
		private byte* data;
		private int   buf_pos;
		private int   length;

		private int bitpack_idx;
		private int bitpack_pos;

		public int BufferPos
		{
			get { return buf_pos; }
		}

		public bool EndOfData
		{
			get { return buf_pos == length; }
		}

		public int AvailableBytes
		{
			get { return length - buf_pos; }
		}

		public void 
		SetSource(NativeDataWriter dataWriter)
		{
			data = dataWriter.data.buffer;
			buf_pos = 0;
			length = dataWriter.data.length;
		}
		
		public void
		SetSource(GrowableBuffer buffer)
		{
			this.data = buffer.buffer;
			length   	= buffer.length;
			
		}

		public void 
		SetSource(byte* source, int length)
		{			
			data = source;
			buf_pos = 0;
			this.length = length;
		}

		public void 
		SetSource(byte* source, int offset, int length)
		{
			data = source;
			buf_pos = offset;
			this.length = length;
		}

		public NativeDataReader(byte* source, int length)
		{
			data = source;
			buf_pos = 0;
			this.length = length;

			bitpack_idx = bitpack_pos = 0;
		}

		public NativeDataReader(byte* source, int offset, int length)
		{
			data = source;
			buf_pos = offset;
			this.length = length;
			
			bitpack_idx = bitpack_pos = 0;
		}

		#region GetMethods
		public byte 
		GetByte()
		{
			if(!CheckBounds(1)) { return default; };
			
			byte result = *(byte*)((IntPtr) data + buf_pos);
			buf_pos += 1;
			return result;
		}
		
		public byte
		GetPackedBits(int num_bits)
		{
			if(num_bits <= 0 || num_bits >= 8) { throw new ArgumentException("num_bits must be between 0 and 7, inclusive."); }
			if(!CheckBounds(1)) { return default; }
			
			// Move to the next bitpack byte.
			if(bitpack_pos == 0)
			{
				bitpack_idx = buf_pos;
				buf_pos 	= buf_pos + 1;
			}

			var byteval = *(byte*)data + buf_pos;
			
			var mask = ~(byte)(255 << buf_pos);
			var res  = (byte)((byteval >> buf_pos) & mask);

			return res;
		}

		public sbyte 
		GetSByte()
		{
			if(!CheckBounds(1)) { return default; };
			
			sbyte result = *(sbyte*)((IntPtr) data + buf_pos);
			buf_pos++;
			return result;
		}

		public bool 
		GetBool()
		{
			if(!CheckBounds(1)) { return default; }
			
			byte result = *(byte*)((IntPtr) data + buf_pos);
			buf_pos += 1;
			return result > 0;
		}

		public ushort GetUShort()
		{
			if(!CheckBounds(2)) { return default; };
			
			ushort result = *(ushort*)((IntPtr) data + buf_pos);
			return result;
		}

		public short GetShort()
		{
			if(!CheckBounds(2)) { return default; };
			
			short result = *(short*)((IntPtr) data + buf_pos);
			buf_pos += 2;
			return result;
		}
		
		public ushort*
		GetChars(uint msg_len)
		{
			var sz = (int)(UnsafeUtility.SizeOf<ushort>() * msg_len);
			
			if(!CheckBounds(sz))
			{
				return null;
			}

			ushort* txt = (ushort*)UnsafeUtility.Malloc(sz, UnsafeUtility.AlignOf<ushort>(), Allocator.Temp);

			UnsafeUtility.MemCpy(txt, data + buf_pos, sz);
			
			buf_pos += sz;

			return txt;
		}

		public long 
		GetLong()
		{
			if(!CheckBounds(8)) { return default; };
			
			long result = *(long*)((IntPtr) data + buf_pos);
			buf_pos += 8;
			return result;
		}

		public ulong 
		GetULong()
		{
			if(!CheckBounds(8)) { return default; };
			
			ulong result = *(ulong*)((IntPtr) data + buf_pos);
			buf_pos += 8;
			return result;
		}

		public int 
		GetInt()
		{
			if(!CheckBounds(4)) { return default; };

			int result = *(int*)((IntPtr) data + buf_pos);
			
			buf_pos += 4;
			return result;
		}
		
		// http://sqlite.org/src4/doc/trunk/www/varint.wiki
		public UInt32 
		GetPackedUInt32()
		{
			byte a0 = GetByte();
			if (a0 < 241)
			{
				return a0;
			}
			byte a1 = GetByte();
			if (a0 >= 241 && a0 <= 248)
			{
				return (UInt32)(240 + 256 * (a0 - 241) + a1);
			}
			byte a2 = GetByte();
			if (a0 == 249)
			{
				return (UInt32)(2288 + 256 * a1 + a2);
			}
			byte a3 = GetByte();
			if (a0 == 250)
			{
				return a1 + (((UInt32)a2) << 8) + (((UInt32)a3) << 16);
			}
			byte a4 = GetByte();
			if (a0 >= 251)
			{
				return a1 + (((UInt32)a2) << 8) + (((UInt32)a3) << 16) + (((UInt32)a4) << 24);
			}
			
			throw new IndexOutOfRangeException("ReadPackedUInt32() failure: " + a0);
		}

		public UInt64 
		GetPackedUInt64()
		{
			byte a0 = GetByte();
			if (a0 < 241)
			{
				return a0;
			}
			byte a1 = GetByte();
			if (a0 >= 241 && a0 <= 248)
			{
				return 240 + 256 * (a0 - ((UInt64)241)) + a1;
			}
			byte a2 = GetByte();
			if (a0 == 249)
			{
				return 2288 + (((UInt64)256) * a1) + a2;
			}
			byte a3 = GetByte();
			if (a0 == 250)
			{
				return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16);
			}
			byte a4 = GetByte();
			if (a0 == 251)
			{
				return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24);
			}

			byte a5 = GetByte();
			if (a0 == 252)
			{
				return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32);
			}

			byte a6 = GetByte();
			if (a0 == 253)
			{
				return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32) + (((UInt64)a6) << 40);
			}

			byte a7 = GetByte();
			if (a0 == 254)
			{
				return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32) + (((UInt64)a6) << 40) + (((UInt64)a7) << 48);
			}

			byte a8 = GetByte();
			if (a0 == 255)
			{
				return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32) + (((UInt64)a6) << 40) + (((UInt64)a7) << 48)  + (((UInt64)a8) << 56);
			}
			
			throw new IndexOutOfRangeException("ReadPackedUInt64() failure: " + a0);
		}

		public uint 
		GetUInt()
		{
			if(!CheckBounds(4)) { return default; };
			
			uint result = *(uint*)((IntPtr) data + buf_pos);
			buf_pos += 4;
			return result;
		}

		public float 
		GetFloat()
		{
			if(!CheckBounds(4)) { return default; };
			
			float result = *(float*)((IntPtr) data + buf_pos);
			buf_pos += 4;
			return result;
		}
		
		public float2
		GetFloat2()
		{
			if(!CheckBounds(8)) { return default; }

			float2 result = *(float2*)((IntPtr) data + buf_pos);
			buf_pos += 8;
			return result;
		}
		
		public float3
		GetFloat3()
		{
			if(!CheckBounds(12)) { return default; };

			float3 result = *(float3*)((IntPtr) data + buf_pos);
			buf_pos += 12;
			return result;
		}

		public double 
		GetDouble()
		{
			if(!CheckBounds(8)) { return default; };
			
			double result = *(double*)((IntPtr) data + buf_pos);
			buf_pos += 8;
			return result;
		}
		
		public void*
		GetBytesCopy<T>(int len, Allocator allocator) where T : struct
		{
			int sz  = UnsafeUtility.SizeOf<T>() * len;
			
			CheckBounds(sz);
			
			var buf = UnsafeUtility.Malloc(sz, UnsafeUtility.AlignOf<T>(), allocator);

			buf_pos += sz;
			
			return buf;
		}
		
		public NavmeshNode
		GetNavmeshNode()
		{
			int sz = UnsafeUtility.SizeOf<NavmeshNode>();
			CheckBounds(sz);

			NavmeshNode result = *(NavmeshNode*)((IntPtr) data + buf_pos);
			buf_pos += sz;
			return result;
		}
		#endregion
		
		private bool
		CheckBounds(int sz)
		{
			return buf_pos + sz < length;
		}

		#region PeekMethods

		public byte 
		PeekByte()
		{
			return ((byte*)data)[buf_pos];
		}

		public sbyte 
		PeekSByte()
		{
			return ((sbyte*)data)[buf_pos];
		}

		public bool 
		PeekBool()
		{
			return ((byte*)data)[buf_pos] > 0;
		}

		public ushort 
		PeekUShort()
		{
			return ((ushort*)data)[buf_pos];
		}

		public short 
		PeekShort()
		{
			return ((short*)data)[buf_pos];
		}

		public long 
		PeekLong()
		{
			return ((long*)data)[buf_pos];
		}

		public ulong 
		PeekULong()
		{
			return ((ulong*)data)[buf_pos];
		}

		public int 
		PeekInt()
		{
			return ((int*)data)[buf_pos];
		}

		public uint 
		PeekUInt()
		{
			return ((uint*)data)[buf_pos];
		}

		public float 
		PeekFloat()
		{
			return ((float*)data)[buf_pos];
		}

		public double 
		PeekDouble()
		{
			return ((double*)data)[buf_pos];
		}
		#endregion

		public void 
		Clear()
		{
			buf_pos = 0;
			length = 0;
			data = null;
		}
	}
}

