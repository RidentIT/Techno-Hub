import type { Metadata } from "next";

import "./globals.css";

export const metadata: Metadata = {
  title: "Techno Hub — Computer & Tech Hardware",
  description:
    "Browse computer parts and peripherals, and build a custom PC quotation — no account needed.",
};

/**
 * Root layout for the public site.
 *
 * There is deliberately no auth provider, session cookie or token handling anywhere in this app.
 * Visitors browse the catalogue and build quotations anonymously, and this app must never call an
 * /api/staff/** route.
 */
export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className="min-h-screen font-sans antialiased">{children}</body>
    </html>
  );
}
