import 'dart:convert';
import 'package:http/http.dart' as http;

/// Signaling client. The server is the SDP offerer: we POST /offer to get its offer, then
/// POST /answer with our answer. Raises on non-200 so the UI can surface errors.
class SignalingClient {
  final String baseUrl;
  final String pin;
  SignalingClient(this.baseUrl, this.pin);

  Future<String> requestOffer() async {
    final resp = await http
        .post(Uri.parse('$baseUrl/offer'),
            headers: const {'Content-Type': 'application/json'},
            body: jsonEncode({'pin': pin}))
        .timeout(const Duration(seconds: 5));

    if (resp.statusCode == 403) throw Exception('PIN 不正确');
    if (resp.statusCode != 200) throw Exception('获取 offer 失败 (${resp.statusCode})');
    return (jsonDecode(resp.body) as Map<String, dynamic>)['sdp'] as String;
  }

  Future<void> submitAnswer(String answerSdp) async {
    final resp = await http
        .post(Uri.parse('$baseUrl/answer'),
            headers: const {'Content-Type': 'application/json'},
            body: jsonEncode({'sdp': answerSdp, 'type': 'answer', 'pin': pin}))
        .timeout(const Duration(seconds: 5));

    if (resp.statusCode != 200) throw Exception('提交 answer 失败 (${resp.statusCode})');
  }
}
