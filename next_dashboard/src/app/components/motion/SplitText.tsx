"use client";

import { motion, useInView, useAnimation } from "framer-motion";
import type { ElementType } from "react";
import { useEffect, useRef } from "react";

interface SplitTextProps {
  text: string;
  as?: ElementType;
  className?: string;
  mode?: "chars" | "words";
  stagger?: number;
  delay?: number;
}

export const SplitText = ({
  text,
  as: Tag = "div",
  className = "",
  mode = "chars",
  stagger = 0.03,
  delay = 0,
}: SplitTextProps) => {
  const ref = useRef<HTMLElement | null>(null);
  const isInView = useInView(ref, { once: true });
  const mainControls = useAnimation();

  useEffect(() => {
    if (isInView) {
      mainControls.start("visible");
    }
  }, [isInView, mainControls]);

  const splitByMode = () => {
    if (mode === "words") {
      return text.split(" ");
    }
    return text.split("");
  };

  const elements = splitByMode();

  return (
    <Tag ref={ref as never} className={className}>
      {elements.map((element, i) => (
        <motion.span
          key={i}
          variants={{
            hidden: { opacity: 0, y: 20 },
            visible: { opacity: 1, y: 0 },
          }}
          initial="hidden"
          animate={mainControls}
          transition={{
            duration: 0.5,
            delay: delay + i * stagger,
          }}
          style={{ display: "inline-block" }}
        >
          {element === " " ? "\u00A0" : element}
          {mode === "words" && i < elements.length - 1 ? "\u00A0" : ""}
        </motion.span>
      ))}
    </Tag>
  );
};
