/**
 * TCP listener built on Bun.listen.
 *
 * Bun hands each socket a `data` object, which is used to carry the Session so
 * no external map is needed.
 */

import { Session, type SessionConfig } from "./session.ts";

interface SocketData {
  session: Session;
}

export interface ListenerOptions extends SessionConfig {
  readonly hostname: string;
  readonly port: number;
}

export function listen(options: ListenerOptions) {
  const { hostname, port, log } = options;

  return Bun.listen<SocketData>({
    hostname,
    port,
    socket: {
      open(socket) {
        const remote = `${socket.remoteAddress}:${port}`;
        socket.data = {
          session: new Session(
            {
              write: (bytes) => {
                socket.write(bytes);
              },
              close: () => {
                socket.end();
              },
              remote,
            },
            options,
          ),
        };
        log(`${remote}: connected`);
        // 694 is the login trigger; the client waits for it before sending 682.
        socket.data.session.greet();
      },

      data(socket, chunk) {
        socket.data.session.onData(new Uint8Array(chunk));
      },

      close(socket) {
        log(`${socket.remoteAddress}: disconnected`);
      },

      error(socket, error) {
        log(`${socket.remoteAddress}: socket error — ${error.message}`);
      },
    },
  });
}
