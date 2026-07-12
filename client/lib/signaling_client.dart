import 'dart:convert';
import 'package:http/http.dart' as http';

/// Minimal signaling client: POSTs a WebRTC offer to the PC's `/offer` endpoint with the
/// pairing PIN and returns the answer SDP. Raises on non-200 so the UI can surface errors.
class SignalingClient {
  final String baseUrl;
  final String pin;
  SignalingClient(this.baseUrl, this.pin);

  Future<String> createAnswer(String offerSdp) async {
    final resp = await http
        .post(
          Uri.parse('$baseUrl/offer'),
          headers: const {'Content-Type': 'application/json'},
          body: jsonEncode({'sdp': offerSdp, 'type': 'offer', 'pin': pin}),
        )
        .timeout(const Duration(seconds: 5));

    if (resp.statusCode == 403) {
      throw Exception('PIN 不正确');
    }
    if (resp.statusCode != 200) {
      throw Exception('连接失败 (${resp.statusCode})');
    }

    final data = jsonDecode(resp.body) as Map<String, dynamic>;
    return data['sdp'] as String;
  }
}
