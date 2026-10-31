#requires -Version 5.1

# Local development signing only. This never modifies Windows trust stores.
function Get-SamEllaSigningCertificate
{
  param([switch]$SelfSign, [string]$Thumbprint)

  if ($SelfSign -and $Thumbprint)
  {
    throw 'Use either -SelfSign or -SigningCertificateThumbprint, not both.'
  }

  if ($SelfSign)
  {
    $friendlyName = 'SamElla local development code signing'
    $certificate = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
      Where-Object { $_.FriendlyName -eq $friendlyName -and $_.HasPrivateKey -and
        $_.NotBefore -le (Get-Date) -and $_.NotAfter -gt (Get-Date).AddDays(1) } |
      Sort-Object NotAfter -Descending | Select-Object -First 1

    if (-not $certificate)
    {
      $certificate = New-SelfSignedCertificate -Type CodeSigningCert `
        -Subject 'CN=Suess Labs (Self-signed development)' -FriendlyName $friendlyName `
        -CertStoreLocation Cert:\CurrentUser\My -KeyAlgorithm RSA -KeyLength 3072 `
        -HashAlgorithm SHA256 -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(3)
    }
  }
  else
  {
    if ($Thumbprint -notmatch '^[0-9a-fA-F]{40}$')
    {
      throw 'SigningCertificateThumbprint must be a 40-character certificate thumbprint.'
    }

    $certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$Thumbprint" -ErrorAction Stop
  }

  if (-not $certificate.HasPrivateKey -or $certificate.NotBefore -gt (Get-Date) -or
    $certificate.NotAfter -le (Get-Date))
  {
    throw 'The signing certificate must be currently valid and have a private key.'
  }

  if ('1.3.6.1.5.5.7.3.3' -notin @($certificate.EnhancedKeyUsageList | ForEach-Object { $_.ObjectId }))
  {
    throw 'The certificate must have the Code Signing enhanced key usage.'
  }

  return $certificate
}

function Get-SamEllaSignTool
{
  $command = Get-Command signtool.exe -ErrorAction SilentlyContinue

  if ($command)
  {
    return $command.Source
  }

  $sdkBin = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
  $tools = @(Get-ChildItem -LiteralPath $sdkBin -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^10\.\d+\.\d+\.\d+$' } |
    Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object { Join-Path $_.FullName 'x64/signtool.exe' } |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })

  if (-not $tools.Count)
  {
    throw 'Signing requires signtool.exe. Install the Windows SDK signing tools or add signtool.exe to PATH.'
  }

  return $tools[0]
}

function Assert-SamEllaSignature
{
  param([string]$Path, [string]$Thumbprint)

  $signature = Get-AuthenticodeSignature -LiteralPath $Path

  if (-not ('SamElla.Build.SignatureVerifier' -as [type]))
  {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace SamElla.Build
{
  public static class SignatureVerifier
  {
    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern uint WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileInfo
    {
      public uint Size;
      [MarshalAs(UnmanagedType.LPWStr)] public string Path;
      public IntPtr Handle;
      public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
      public uint Size;
      public IntPtr Policy;
      public IntPtr Sip;
      public uint UI;
      public uint Revocation;
      public uint Choice;
      public IntPtr File;
      public uint StateAction;
      public IntPtr State;
      public IntPtr Url;
      public uint Flags;
      public uint UIContext;
      public IntPtr SignatureSettings;
    }

    public static uint Verify(string path)
    {
      var file = new FileInfo { Size = (uint)Marshal.SizeOf(typeof(FileInfo)), Path = path };
      IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(FileInfo)));
      Marshal.StructureToPtr(file, pointer, false);

      try
      {
        var data = new TrustData
        {
          Size = (uint)Marshal.SizeOf(typeof(TrustData)),
          UI = 2, Choice = 1, File = pointer,
          // No revocation/network lookups for this local integrity check.
          Flags = 0x1010
        };
        var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        return WinVerifyTrust(new IntPtr(-1), ref action, ref data);
      }
      finally
      {
        Marshal.DestroyStructure(pointer, typeof(FileInfo));
        Marshal.FreeHGlobal(pointer);
      }
    }
  }
}
'@
  }

  # PowerShell may report an untrusted self-signed root as UnknownError. Check
  # WinVerifyTrust directly so we allow only success or CERT_E_UNTRUSTEDROOT,
  # and still reject bad digests, missing signatures, and every other failure.
  $verification = [SamElla.Build.SignatureVerifier]::Verify([IO.Path]::GetFullPath($Path))

  if (-not $signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $Thumbprint -or
    $verification -notin @([uint32]0, [uint32]2148204809))
  {
    throw ("Signing verification failed for '{0}': {1} (0x{2:X8})." -f $Path, $signature.Status, $verification)
  }
}
