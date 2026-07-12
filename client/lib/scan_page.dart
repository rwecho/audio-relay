import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import 'player_page.dart';

class ScanPage extends StatefulWidget {
  const ScanPage({super.key});
  @override
  State<ScanPage> createState() => _ScanPageState();
}

class _ScanPageState extends State<ScanPage> {
  final MobileScannerController _controller = MobileScannerController();
  bool _handled = false;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _onDetect(BarcodeCapture capture) {
    if (_handled) return;
    final list = capture.barcodes;
    if (list.isEmpty) return;
    final code = list.first.rawValue;
    if (code == null) return;
    final parsed = _parse(code);
    if (parsed == null) return;
    _handled = true;
    Navigator.push(
      context,
      MaterialPageRoute(builder: (_) => PlayerPage(url: parsed.$1, pin: parsed.$2)),
    ).then((_) => _handled = false);
  }

  /// Splits the scanned payload (http://host:port?pin=XXXX) into (url, pin).
  static (String, String)? _parse(String payload) {
    final q = payload.indexOf('?');
    String url = q >= 0 ? payload.substring(0, q) : payload;
    String pin = '';
    if (q >= 0) {
      for (final part in payload.substring(q + 1).split('&')) {
        final kv = part.split('=');
        if (kv.length == 2 && kv[0] == 'pin') pin = Uri.decodeComponent(kv[1]);
      }
    }
    if (url.isEmpty || pin.isEmpty) return null;
    return (url, pin);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      body: Stack(
        children: [
          MobileScanner(controller: _controller, onDetect: _onDetect),
          Center(
            child: Container(
              width: 240,
              height: 240,
              decoration: BoxDecoration(
                border: Border.all(color: Colors.white54, width: 2),
                borderRadius: BorderRadius.circular(16),
              ),
            ),
          ),
          const Positioned(
            top: 64,
            left: 0,
            right: 0,
            child: Text(
              '将电脑上的二维码对准取景框',
              textAlign: TextAlign.center,
              style: TextStyle(color: Colors.white, fontSize: 15),
            ),
          ),
        ],
      ),
    );
  }
}
