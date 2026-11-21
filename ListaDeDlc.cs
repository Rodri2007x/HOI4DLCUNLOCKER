using System.Runtime.InteropServices;

namespace DlcDeHoi4;

internal sealed class ListaDeDlc : ListView
{
    private const int LvmGetHeader = 0x101F;
    private const int LvmGetGroupRect = 0x1062;
    private const int LvmGetGroupInfo = 0x1095;
    private const int WmReflectNotify = 0x204E;
    private const int NmCustomDraw = -12;
    private const int CddsPrePaint = 0x0001;
    private const int CddsPostPaint = 0x0002;
    private const int CdrfNotifyPostPaint = 0x0010;
    private const int LvcdiGroup = 1;
    private const int LvggrLabel = 2;
    private const int LvgfHeader = 0x0001;
    private bool temaOscuro;

    public void AplicarTema(bool oscuro)
    {
        temaOscuro = oscuro;
        BackColor = Color.FromArgb(SystemColors.Window.ToArgb());
        ForeColor = Color.FromArgb(SystemColors.WindowText.ToArgb());
        if (IsHandleCreated)
        {
            AplicarTemaNativo();
        }
    }

    protected override void OnHandleCreated(EventArgs argumentos)
    {
        base.OnHandleCreated(argumentos);
        if (temaOscuro)
        {
            AplicarTemaNativo();
        }
    }

    private void AplicarTemaNativo()
    {
        // Explorer incluye la barra oscura. ItemsView aplica el estilo de las columnas.
        _ = SetWindowTheme(Handle, temaOscuro ? "DarkMode_Explorer" : null, null);
        nint encabezado = SendMessage(Handle, LvmGetHeader, 0, 0);
        if (encabezado != 0)
        {
            _ = SetWindowTheme(encabezado, temaOscuro ? "DarkMode_ItemsView" : null, null);
        }
    }

    protected override void WndProc(ref Message mensaje)
    {
        base.WndProc(ref mensaje);
        if (!temaOscuro || mensaje.Msg != WmReflectNotify || mensaje.LParam == 0)
        {
            return;
        }

        NMHDR encabezado = Marshal.PtrToStructure<NMHDR>(mensaje.LParam);
        if (encabezado.code != NmCustomDraw || encabezado.hwndFrom != mensaje.HWnd)
        {
            return;
        }

        NMLVCUSTOMDRAW dibujo = Marshal.PtrToStructure<NMLVCUSTOMDRAW>(mensaje.LParam);
        if (dibujo.dwItemType != LvcdiGroup)
        {
            return;
        }

        switch (dibujo.nmcd.dwDrawStage)
        {
            case CddsPrePaint:
                mensaje.Result |= CdrfNotifyPostPaint;
                break;
            case CddsPostPaint:
                PintarTituloDeGrupo((int)dibujo.nmcd.dwItemSpec, dibujo.nmcd.hdc);
                break;
        }
    }

    private void PintarTituloDeGrupo(int id, nint contexto)
    {
        var rectangulo = new RECT { top = LvggrLabel };
        if (SendMessage(Handle, LvmGetGroupRect, id, ref rectangulo) == 0 ||
            ObtenerTituloDeGrupo(id) is not { Length: > 0 } titulo)
        {
            return;
        }

        Rectangle limites = Rectangle.FromLTRB(rectangulo.left, rectangulo.top, rectangulo.right, rectangulo.bottom);
        using var graficos = Graphics.FromHdc(contexto);
        using var fondo = new SolidBrush(BackColor);
        // Explorer dibuja el título con el color del tema claro.
        graficos.FillRectangle(fondo, limites);
        TextRenderer.DrawText(graficos, titulo, Font, limites, ForeColor,
            TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
            TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsClipping);
    }

    private string? ObtenerTituloDeGrupo(int id)
    {
        int capacidad = 1;
        foreach (ListViewGroup grupo in Groups)
        {
            capacidad = Math.Max(capacidad, grupo.Header.Length + 1);
        }

        nint memoria = Marshal.AllocHGlobal(capacidad * sizeof(char));
        try
        {
            Marshal.WriteInt16(memoria, 0);
            Marshal.WriteInt16(memoria, (capacidad - 1) * sizeof(char), 0);
            var grupo = new LVGROUP
            {
                cbSize = (uint)Marshal.SizeOf<LVGROUP>(),
                mask = LvgfHeader,
                pszHeader = memoria,
                cchHeader = capacidad,
            };
            return SendMessage(Handle, LvmGetGroupInfo, id, ref grupo) == -1 ? null : Marshal.PtrToStringUni(memoria);
        }
        finally
        {
            Marshal.FreeHGlobal(memoria);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NMHDR
    {
        public nint hwndFrom;
        public nuint idFrom;
        public int code;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NMCUSTOMDRAW
    {
        public NMHDR hdr;
        public uint dwDrawStage;
        public nint hdc;
        public RECT rc;
        public nuint dwItemSpec;
        public uint uItemState;
        public nint lItemlParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NMLVCUSTOMDRAW
    {
        public NMCUSTOMDRAW nmcd;
        public int clrText;
        public int clrTextBk;
        public int iSubItem;
        public uint dwItemType;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LVGROUP
    {
        public uint cbSize;
        public uint mask;
        public nint pszHeader;
        public int cchHeader;
        public nint pszFooter;
        public int cchFooter;
        public int iGroupId;
        public uint stateMask;
        public uint state;
        public uint uAlign;
        public nint pszSubtitle;
        public uint cchSubtitle;
        public nint pszTask;
        public uint cchTask;
        public nint pszDescriptionTop;
        public uint cchDescriptionTop;
        public nint pszDescriptionBottom;
        public uint cchDescriptionBottom;
        public int iTitleImage;
        public int iExtendedImage;
        public int iFirstItem;
        public uint cItems;
        public nint pszSubsetTitle;
        public uint cchSubsetTitle;
    }

    [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(nint ventana, string? tema, string? listaDeClases);

    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
    private static extern nint SendMessage(nint ventana, uint mensaje, nint parametroW, nint parametroL);

    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
    private static extern nint SendMessage(nint ventana, uint mensaje, nint parametroW, ref LVGROUP parametroL);

    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
    private static extern nint SendMessage(nint ventana, uint mensaje, nint parametroW, ref RECT parametroL);
}
