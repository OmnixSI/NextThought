"""Generate a valid PDF with an unreferenced stream to exercise file-size limits.
This is capacity coverage only, not a substitute for a complex real paper.
"""
import pathlib,sys
path=pathlib.Path(sys.argv[1]);mib=int(sys.argv[2]);path.parent.mkdir(parents=True,exist_ok=True)
with path.open('wb') as f:
 f.write(b'%PDF-1.7\n%\xe2\xe3\xcf\xd3\n');offsets=[0]
 def obj(n,data):
  offsets.append(f.tell());f.write(str(n).encode()+b' 0 obj\n'+data+b'\nendobj\n')
 obj(1,b'<< /Type /Catalog /Pages 2 0 R >>')
 obj(2,b'<< /Type /Pages /Kids [3 0 R] /Count 1 >>')
 obj(3,b'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>')
 content=b'BT /F1 14 Tf 48 700 Td (Large PDF capacity: exact 1234.56 USD and -7.5 percent) Tj ET'
 obj(4,b'<< /Length '+str(len(content)).encode()+b' >>\nstream\n'+content+b'\nendstream')
 obj(5,b'<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>')
 offsets.append(f.tell());size=mib*1024*1024
 f.write(b'6 0 obj\n<< /Length '+str(size).encode()+b' >>\nstream\n')
 block=b'0'*1024*1024
 for _ in range(mib):f.write(block)
 f.write(b'\nendstream\nendobj\n');xref=f.tell();f.write(b'xref\n0 7\n0000000000 65535 f \n')
 for offset in offsets[1:]:f.write(f'{offset:010d} 00000 n \n'.encode())
 f.write(b'trailer\n<< /Size 7 /Root 1 0 R >>\nstartxref\n'+str(xref).encode()+b'\n%%EOF\n')
print(path.stat().st_size)
