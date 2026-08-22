import type { Metadata } from "next";

import { AuthProvider } from "@/lib/auth-context";

import "./globals.css";

export const metadata: Metadata = {
  title: "Techno Hub — Staff Console",
  description: "Internal business management console for Techno Hub.",
  // An internal tool: keep it out of search results.
  robots: { index: false, follow: false },
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      {/* Tailwind's font-sans is a system stack, so there is no webfont fetch at build time. */}
      <body className="min-h-screen font-sans antialiased">
        {/*
          AuthProvider sits at the root so the session survives client-side navigation between
          /login and /dashboard — remounting it would throw away the in-memory access token.
        */}
        <AuthProvider>{children}</AuthProvider>
      </body>
    </html>
  );
}
