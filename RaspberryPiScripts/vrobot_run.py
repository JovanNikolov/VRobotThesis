import time
from board import SCL, SDA
import busio
from adafruit_pca9685 import PCA9685
import socket
import signal
import sys

UDP_IP = '0.0.0.0'
UDP_PORT = 6000

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind((UDP_IP, UDP_PORT))

i2c = busio.I2C(SCL, SDA)
pca = PCA9685(i2c)
pca.frequency = 50

servo_min = 500
servo_max = 2500

def angle_to_pwm(angle):
    pulse = servo_min + (servo_max - servo_min) * (angle / 180.0)
    duty_cycle = int(pulse * 65536 / 20000)
    return duty_cycle

def stop_servos():
    pca.channels[0].duty_cycle = angle_to_pwm(90) #rPitch
    pca.channels[1].duty_cycle = angle_to_pwm(160) #rRoll
    pca.channels[2].duty_cycle = angle_to_pwm(90) #rYaw
    pca.channels[3].duty_cycle = angle_to_pwm(60) #rElbow
    pca.channels[4].duty_cycle = angle_to_pwm(130) #rWrist
    pca.channels[5].duty_cycle = angle_to_pwm(170) #rGrip
    
    pca.channels[6].duty_cycle = angle_to_pwm(90) #lPitch
    pca.channels[7].duty_cycle = angle_to_pwm(20) #lRoll
    pca.channels[8].duty_cycle = angle_to_pwm(90) #lYaw
    pca.channels[9].duty_cycle = angle_to_pwm(60) #lElbow
    pca.channels[10].duty_cycle = angle_to_pwm(50) #lWrist
    pca.channels[11].duty_cycle = angle_to_pwm(170) #lGrip
    
    pca.channels[12].duty_cycle = angle_to_pwm(90)
    pca.channels[13].duty_cycle = angle_to_pwm(120)
    pca.deinit() 
    time.sleep(1.5)

def signal_handler(sig, frame):
    print("Stopping servos and exiting...")
    stop_servos()
    sys.exit(0)

# Catch SIGINT (Ctrl+C)
signal.signal(signal.SIGINT, signal_handler)

try:
    while True:
        try:
            data, addr = sock.recvfrom(1024)
        except socket.timeout:
            continue
        angles = data.decode().strip().split(',')
        rPitch, rRoll, rYaw, rElbow, rWrist, rGrip, lPitch, lRoll, lYaw, lElbow, lWrist, lGrip, headx, heady = map(float, angles)
        
        pca.channels[0].duty_cycle = angle_to_pwm(rPitch) 
        pca.channels[1].duty_cycle = angle_to_pwm(rRoll) 
        pca.channels[2].duty_cycle = angle_to_pwm(rYaw) 
        pca.channels[3].duty_cycle = angle_to_pwm(rElbow)
        pca.channels[4].duty_cycle = angle_to_pwm(rWrist)
        pca.channels[5].duty_cycle = angle_to_pwm(rGrip)
        
        pca.channels[6].duty_cycle = angle_to_pwm(lPitch) 
        pca.channels[7].duty_cycle = angle_to_pwm(lRoll)
        pca.channels[8].duty_cycle = angle_to_pwm(lYaw)
        pca.channels[9].duty_cycle = angle_to_pwm(lElbow)
        pca.channels[10].duty_cycle = angle_to_pwm(lWrist)
        pca.channels[11].duty_cycle = angle_to_pwm(lGrip)
        
        pca.channels[12].duty_cycle = angle_to_pwm(headx)
        pca.channels[13].duty_cycle = angle_to_pwm(heady)

except KeyboardInterrupt:
    print("Interrupt received, stopping servos...")
    stop_servos()
    pca.deinit()

