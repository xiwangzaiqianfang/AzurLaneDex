using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AzurLaneDex.Services
{
    /// <summary>
    /// 极简 SQLite **只读**访问层：P/Invoke Windows 自带的 winsqlite3.dll（零 NuGet 依赖）。
    /// 只实现装载静态数据所需的最小集合：打开 / 预处理 / 单步 / 取列 / 绑定参数 / 关闭。
    /// </summary>
    internal static class NativeSqlite
    {
        private const string Lib = "winsqlite3.dll";

        internal const int SQLITE_OK = 0;
        internal const int SQLITE_ROW = 100;
        internal const int SQLITE_DONE = 101;

        // 打开标志：只读
        internal const int SQLITE_OPEN_READONLY = 0x00000001;
        // 绑定文本的析构器：SQLITE_TRANSIENT（让 SQLite 自行复制）
        internal static readonly IntPtr SQLITE_TRANSIENT = new IntPtr(-1);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_open_v2(byte[] fileName, out IntPtr db, int flags, IntPtr vfs);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_close(IntPtr db);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr sqlite3_errmsg(IntPtr db);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int numBytes, out IntPtr stmt, IntPtr tail);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_step(IntPtr stmt);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_finalize(IntPtr stmt);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_bind_text(IntPtr stmt, int index, byte[] value, int numBytes, IntPtr destructor);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_bind_int64(IntPtr stmt, int index, long value);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_bind_null(IntPtr stmt, int index);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_column_type(IntPtr stmt, int col);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr sqlite3_column_text(IntPtr stmt, int col);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_column_bytes(IntPtr stmt, int col);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern long sqlite3_column_int64(IntPtr stmt, int col);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern double sqlite3_column_double(IntPtr stmt, int col);

        internal static string? ErrorMessage(IntPtr db)
        {
            try
            {
                IntPtr p = sqlite3_errmsg(db);
                return p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
            }
            catch
            {
                return null;
            }
        }

        internal static byte[] Utf8Z(string s)
        {
            // 末尾补 NUL，非空串
            byte[] bytes = Encoding.UTF8.GetBytes(s ?? "");
            var withNul = new byte[bytes.Length + 1];
            Buffer.BlockCopy(bytes, 0, withNul, 0, bytes.Length);
            return withNul;
        }
    }

    /// <summary>只读查询结果：ForwardOnly 游标 + 列取值/参数绑定。</summary>
    internal sealed class SqliteQuery : IDisposable
    {
        private IntPtr _db;
        private IntPtr _stmt;
        private bool _disposed;

        internal SqliteQuery(IntPtr db, string sql, params object?[] args)
        {
            _db = db;
            byte[] sqlBytes = NativeSqlite.Utf8Z(sql);
            int rc = NativeSqlite.sqlite3_prepare_v2(db, sqlBytes, sqlBytes.Length - 1, out _stmt, IntPtr.Zero);
            if (rc != NativeSqlite.SQLITE_OK || _stmt == IntPtr.Zero)
            {
                string msg = NativeSqlite.ErrorMessage(db) ?? "";
                throw new InvalidOperationException($"SQLite prepare 失败({rc}): {msg}\nSQL: {sql}");
            }

            for (int i = 0; i < args.Length; i++)
            {
                int idx = i + 1;
                switch (args[i])
                {
                    case null:
                        NativeSqlite.sqlite3_bind_null(_stmt, idx);
                        break;
                    case string s:
                        byte[] b = NativeSqlite.Utf8Z(s);
                        NativeSqlite.sqlite3_bind_text(_stmt, idx, b, b.Length - 1, NativeSqlite.SQLITE_TRANSIENT);
                        break;
                    case int n:
                        NativeSqlite.sqlite3_bind_int64(_stmt, idx, n);
                        break;
                    case long l:
                        NativeSqlite.sqlite3_bind_int64(_stmt, idx, l);
                        break;
                    case bool bo:
                        NativeSqlite.sqlite3_bind_int64(_stmt, idx, bo ? 1 : 0);
                        break;
                    default:
                        NativeSqlite.sqlite3_bind_text(_stmt, idx, NativeSqlite.Utf8Z(args[i]!.ToString()), -1, NativeSqlite.SQLITE_TRANSIENT);
                        break;
                }
            }
        }

        public bool Read() => NativeSqlite.sqlite3_step(_stmt) == NativeSqlite.SQLITE_ROW;

        public bool IsNull(int col) => NativeSqlite.sqlite3_column_type(_stmt, col) == 5 /* SQLITE_NULL */;

        public string? Text(int col)
        {
            if (IsNull(col)) return null;
            IntPtr p = NativeSqlite.sqlite3_column_text(_stmt, col);
            if (p == IntPtr.Zero) return null;
            int len = NativeSqlite.sqlite3_column_bytes(_stmt, col);
            return len <= 0 ? "" : Marshal.PtrToStringUTF8(p, len);
        }

        public long Long(int col, long fallback = 0)
        {
            if (IsNull(col)) return fallback;
            return NativeSqlite.sqlite3_column_int64(_stmt, col);
        }

        public int Int(int col, int fallback = 0) => (int)Long(col, fallback);

        public double Double(int col, double fallback = 0)
        {
            if (IsNull(col)) return fallback;
            return NativeSqlite.sqlite3_column_double(_stmt, col);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_stmt != IntPtr.Zero)
            {
                NativeSqlite.sqlite3_finalize(_stmt);
                _stmt = IntPtr.Zero;
            }
        }
    }

    /// <summary>数据库句柄：打开（只读）/ 查询 / 关闭。</summary>
    internal sealed class SqliteDatabase : IDisposable
    {
        private IntPtr _db;

        private SqliteDatabase(IntPtr db) => _db = db;

        public bool IsOpen => _db != IntPtr.Zero;

        public static SqliteDatabase OpenReadOnly(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("SQLite 数据库不存在", path);

            int rc = NativeSqlite.sqlite3_open_v2(NativeSqlite.Utf8Z(path), out IntPtr db, NativeSqlite.SQLITE_OPEN_READONLY, IntPtr.Zero);
            if (rc != NativeSqlite.SQLITE_OK || db == IntPtr.Zero)
            {
                string msg = NativeSqlite.ErrorMessage(db) ?? "";
                if (db != IntPtr.Zero) NativeSqlite.sqlite3_close(db);
                throw new InvalidOperationException($"SQLite 打开失败({rc}): {msg} 路径: {path}");
            }
            return new SqliteDatabase(db);
        }

        public SqliteQuery Query(string sql, params object?[] args)
            => new SqliteQuery(_db, sql, args);

        public void Dispose()
        {
            if (_db == IntPtr.Zero) return;
            NativeSqlite.sqlite3_close(_db);
            _db = IntPtr.Zero;
        }
    }
}
