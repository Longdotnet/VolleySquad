// ============================================================
// useMatchHub - Custom Hook: Kết nối SignalR Real-time
// ============================================================
//
// CUSTOM HOOK là gì?
//   Hàm JavaScript bắt đầu bằng "use" và gọi các built-in hooks (useState, useEffect...).
//   Mục đích: Tái sử dụng stateful logic giữa các component.
//   Ví dụ: useMatchHub() đóng gói toàn bộ logic connect/disconnect SignalR
//          → DashboardPage.tsx chỉ cần 1 dòng: useMatchHub(matchId, onSlotUpdated)
//
// TẠI SAO SIGNALR thay vì Polling?
//   Polling cũ:  FE cứ mỗi 3 giây gọi GET /api/match/{id} để kiểm tra xem có cập nhật không
//                Vấn đề: 95% request là "không có gì mới" → lãng phí bandwidth, server tải
//
//   SignalR:     Server CHỦ ĐỘNG push message xuống FE khi có thay đổi thật sự
//                Protocol hierarchy (thử lần lượt):
//                  1. WebSocket (ưu tiên nhất — full-duplex, low latency)
//                  2. Server-Sent Events (SSE — server → client only)
//                  3. Long Polling (fallback khi không hỗ trợ 2 cái trên)
//
// TẠI SAO useRef thay vì useState cho connectionRef?
//   useState:  Lưu giá trị + kích hoạt re-render khi thay đổi
//   useRef:    Lưu giá trị mà KHÔNG kích hoạt re-render (giữ reference qua các renders)
//   Connection object là internal implementation detail, không liên quan đến UI
//   → Không cần re-render khi connection thay đổi → useRef là đúng.
//
// ============================================================
// useEffect CLEANUP FUNCTION
// ============================================================
// useEffect(() => {
//   // Setup code
//   return () => {
//     // CLEANUP CODE — chạy khi component unmount hoặc deps thay đổi
//   };
// }, [deps]);
//
// Nếu không có cleanup:
//   User navigate đến trang khác → component unmount
//   → Connection vẫn còn sống → memory leak + nhận event không cần thiết
//   → Nếu user quay lại → tạo connection mới → có 2 connection cùng lúc → duplicate events!
import { useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuthStore } from '../store/authStore';

export interface SlotUpdatePayload {
  matchId: string;
  registeredCount: number;
  maxSlots: number;
}

// ============================================================
// TYPESCRIPT FUNCTION SIGNATURE
// ============================================================
// matchId: string | null  — null khi chưa có match được chọn
// onSlotUpdated: callback function — pattern Observer/Event Handler
//   Dùng callback thay vì return value vì đây là async event (không biết khi nào xảy ra)
export function useMatchHub(matchId: string | null, onSlotUpdated: (data: SlotUpdatePayload) => void) {
  const token = useAuthStore((s) => s.token);

  // useRef<T>(initialValue): Generic type T = signalR.HubConnection | null
  // Truy cập giá trị: connectionRef.current
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    // Guard clause: không kết nối nếu chưa có matchId hoặc token
    if (!matchId || !token) return;

    // ============================================================
    // BUILDER PATTERN - Cấu hình kết nối từng bước
    // ============================================================
    // Builder Pattern: Xây dựng đối tượng phức tạp qua các bước (method chaining).
    // Thay vì truyền 10 tham số vào constructor → gọi .withUrl().withReconnect()...
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${import.meta.env.VITE_SIGNALR_URL ?? 'https://localhost:7202'}/hubs/match`, {
        // accessTokenFactory: Hàm trả về token để SignalR gắn vào WebSocket handshake.
        // WS không hỗ trợ custom header trong browser → dùng query string hoặc factory này.
        // SignalR backend đọc từ query string và treat như Authorization header.
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000]) // Retry sau: 0ms, 2s, 5s, 10s
      .configureLogging(signalR.LogLevel.Warning) // Giảm noise trong console
      .build();

    connectionRef.current = connection;

    // Đăng ký lắng nghe event "SlotUpdated" từ server.
    // Tên event phải khớp chính xác với cái server gọi: SendAsync("SlotUpdated", data)
    connection.on('SlotUpdated', (data: SlotUpdatePayload) => {
      onSlotUpdated(data);
    });

    // Start connection → rồi join vào nhóm của match cụ thể.
    // Server sẽ chỉ gửi "SlotUpdated" cho các client trong nhóm "match-{matchId}".
    connection
      .start()
      .then(() => connection.invoke('JoinMatchGroup', matchId))
      .catch((err) => console.error('SignalR connection error:', err));

    // CLEANUP: Rời nhóm và đóng connection khi component unmount
    return () => {
      connection.invoke('LeaveMatchGroup', matchId).catch(() => {});
      connection.stop();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
    // Giải thích: onSlotUpdated được wrap bằng useCallback ở DashboardPage
    // nên deps chỉ cần matchId và token là đủ để trigger reconnect đúng lúc.
  }, [matchId, token]);
}

