using System;
using System.IO;
using System.Runtime.InteropServices;

namespace JLVisionLib;

internal sealed class JlSerializationBuffer : JlHandle
{
	internal JlSerializationBuffer()
		: base(JlHandleBase.UNDEF)
	{
	}

	private JlSerializationBuffer(IntPtr handle)
		: base(handle)
	{
		if (handle != JlHandleBase.UNDEF)
		{
			AssertSemType();
		}
	}

	private void AssertSemType()
	{
		AssertSemType("serialized_item");
	}

	internal static int LoadNew(IntPtr proc, int parIndex, int err, out JlSerializationBuffer obj)
	{
		obj = new JlSerializationBuffer(JlHandleBase.UNDEF);
		return obj.Load(proc, parIndex, err);
	}

	internal JlSerializationBuffer(byte[] data)
	{
		GCHandle gCHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
		try
		{
			CreateSerializedItemPtr(gCHandle.AddrOfPinnedObject(), data.Length, "true");
		}
		finally
		{
			gCHandle.Free();
		}
	}

	internal static byte[] ToBytes(JlSerializationBuffer item)
	{
		IntPtr serializedItemPtr = item.GetSerializedItemPtr(out var size);
		byte[] array = new byte[size];
		Marshal.Copy(serializedItemPtr, array, 0, size);
		GC.KeepAlive(item);
		return array;
	}

	internal static byte[] LoadBytes(IntPtr proc, int parIndex, int err)
	{
		err = LoadNew(proc, parIndex, err, out JlSerializationBuffer obj);
		if (JlNativeApi.IsFailure(err))
		{
			obj.Dispose();
			return Array.Empty<byte>();
		}
		try
		{
			return ToBytes(obj);
		}
		finally
		{
			obj.Dispose();
		}
	}

	internal static void WriteToStream(byte[] data, Stream stream)
	{
		stream.Write(data, 0, data.Length);
	}

	internal static byte[] ReadFromStream(Stream stream)
	{
		BinaryReader binaryReader = new BinaryReader(stream);
		byte[] array = binaryReader.ReadBytes(16);
		if (array.Length < 16 || JlNativeApi.IsFailure(JlNativeApi.GetSerializedSize(array, out var size)))
		{
			throw new JlException("Input stream is no serialized Vision object");
		}
		// Reject excessively large or malformed lengths before narrowing to int.
		const ulong MaxSerializedPayloadBytes = 256UL * 1024 * 1024;
		if (size > MaxSerializedPayloadBytes || size > int.MaxValue - 16)
		{
			throw new JlException("Input stream too large");
		}
		int payloadSize = checked((int)size);
		byte[] array2 = binaryReader.ReadBytes(payloadSize);
		if (array2.Length != payloadSize)
		{
			throw new JlException("Unexpected end of serialization data");
		}
		byte[] array3 = new byte[checked(payloadSize + 16)];
		array.CopyTo(array3, 0);
		array2.CopyTo(array3, 16);
		return array3;
	}

	private IntPtr GetSerializedItemPtr(out int size)
	{
		IntPtr proc = JlNativeApi.PreCall(403);
		using var nativeCall = JlNativeProcedureScope.Begin(proc);
		Store(proc, 0);
		JlNativeApi.InitOCT(proc, 0);
		JlNativeApi.InitOCT(proc, 1);
		int err = JlNativeApi.CallProcedure(proc);
		err = JlNativeApi.LoadIP(proc, 0, err, out var intPtrValue);
		err = JlNativeApi.LoadI(proc, 1, err, out size);
		nativeCall.Complete(err);
		GC.KeepAlive(this);
		return intPtrValue;
	}

	private void CreateSerializedItemPtr(IntPtr pointer, int size, string copy)
	{
		Dispose();
		IntPtr proc = JlNativeApi.PreCall(404);
		using var nativeCall = JlNativeProcedureScope.Begin(proc);
		JlNativeApi.StoreIP(proc, 0, pointer);
		JlNativeApi.StoreI(proc, 1, size);
		JlNativeApi.StoreS(proc, 2, copy);
		JlNativeApi.InitOCT(proc, 0);
		int err = JlNativeApi.CallProcedure(proc);
		err = Load(proc, 0, err);
		nativeCall.Complete(err);
		GC.KeepAlive(this);
	}
}
