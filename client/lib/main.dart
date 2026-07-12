import 'package:flutter/material.dart';
import 'scan_page.dart';

void main() => runApp(const AudioRelayApp());

class AudioRelayApp extends StatelessWidget {
  const AudioRelayApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Audio Relay',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        brightness: Brightness.dark,
        useMaterial3: true,
        colorSchemeSeed: const Color(0xFF0A84FF),
        fontFamily: null, // system default (SF on iOS, Roboto on Android)
      ),
      home: const ScanPage(),
    );
  }
}
