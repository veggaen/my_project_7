import type { Metadata } from "next";
import { Inter } from "next/font/google";
import "./globals.css";
import { ExtensionNoiseSilencer } from "./components/ExtensionNoiseSilencer";

const inter = Inter({ subsets: ["latin"] });

export const metadata: Metadata = {
  title: "Vegga Roleplay | s&box MMORPG",
  description: "An OSRS-inspired roleplay game built on s&box with 23 skills, custom UI, and multiplayer support",
  keywords: ["s&box", "OSRS", "roleplay", "MMORPG", "sandbox", "multiplayer"],
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className={inter.className}>
        <ExtensionNoiseSilencer />
        {children}
      </body>
    </html>
  );
}
